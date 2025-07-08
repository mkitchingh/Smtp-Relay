using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Reflection;
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
    /// Relays accepted messages to the configured smart-host, with full
    /// SMTP protocol tracing and IP allow-list enforcement.
    /// </summary>
    public sealed class MessageRelayStore : MessageStore
    {
        private readonly Config  _cfg;
        private readonly ILogger _log;

        public MessageRelayStore(Config cfg, ILogger logger)
        {
            _cfg = cfg;
            _log = logger;
        }

        // SmtpServer 9.x signature
        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext        context,
            IMessageTransaction    transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken      cancellationToken)
        {
            // ── Try to extract the remote IP without binding to internals ──
            string clientIp = "unknown";
            try
            {
                PropertyInfo? prop =
                    context.GetType().GetProperty("RemoteEndPoint",
                        BindingFlags.Public | BindingFlags.Instance);

                if (prop?.GetValue(context) is IPEndPoint ep)
                    clientIp = ep.Address.ToString();
            }
            catch
            {
                /* swallow – clientIp stays "unknown" */
            }

            // ── IP allow-list check ──────────────────────────────────────
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return SmtpResponse.AuthenticationRequired;
            }

            _log.LogInformation("Incoming relay request from {IP}", clientIp);

            // ── Re-hydrate MimeMessage from buffer ───────────────────────
            using var ms = new MemoryStream();
            foreach (var segment in buffer)
                ms.Write(segment.Span);
            ms.Position = 0;

            var message = MimeMessage.Load(ms);

            // ── Prepare daily protocol log file ──────────────────────────
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);

            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");

            // ── Outbound SMTP client with MailKit.ProtocolLogger ─────────
            using var smtp = new SmtpClient(new MailKit.ProtocolLogger(protoPath));

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
