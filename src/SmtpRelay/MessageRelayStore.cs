using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NetTools;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using SmtpResponse = SmtpServer.Protocol.SmtpResponse;   // disambiguate

namespace SmtpRelay
{
    /// <summary>
    /// Accepts messages from <see cref="SmtpServer"/> and relays them to the
    /// upstream smart-host defined in <see cref="Config"/>.
    /// </summary>
    public sealed class MessageRelayStore : MessageStore
    {
        private readonly Config _cfg;
        private readonly ILogger _log;

        public MessageRelayStore(Config cfg, ILogger logger)
        {
            _cfg = cfg;
            _log = logger;
        }

        // SmtpServer 9.x signature
        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext context,
            IMessageTransaction transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            var clientIp = context.RemoteEndPoint?.Address?.ToString() ?? "unknown";

            // ── IP allow-list check ────────────────────────────────────
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return SmtpResponse.AuthenticationRequired;
            }

            _log.LogInformation("Incoming relay request from {IP}", clientIp);

            // ── reconstruct MimeMessage from buffer ────────────────────
            using var ms = new MemoryStream();
            foreach (var segment in buffer)
                ms.Write(segment.Span);
            ms.Position = 0;

            var message = MimeMessage.Load(ms);

            // ── wire-level trace file (one per day) ─────────────────────
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);
            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");

            // ── outbound SMTP client ────────────────────────────────────
            using var smtp = new SmtpClient(new ProtocolLogger(protoPath));

            _log.LogInformation("Connecting to {Host}:{Port} (STARTTLS={TLS})",
                _cfg.SmartHost, _cfg.SmartHostPort, _cfg.UseStartTls);

            await smtp.ConnectAsync(
                _cfg.SmartHost,
                _cfg.SmartHostPort,
                _cfg.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable
                                 : SecureSocketOptions.None,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_cfg.Username))
            {
                _log.LogInformation("Authenticating as {User}", _cfg.Username);
                await smtp.AuthenticateAsync(_cfg.Username, _cfg.Password, cancellationToken);
            }

            _log.LogInformation("Sending message from {From} to {To}",
                string.Join(",", message.From), string.Join(",", message.To));

            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            _log.LogInformation("Smarthost relay complete");
            _log.LogInformation("Relayed mail from {IP}", clientIp);

            return SmtpResponse.Ok;
        }
    }
}
