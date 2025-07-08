using System;
using System.Buffers;
using System.Collections;
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
            // ————————————————— 1.  Resolve client IP  ——————————————————
            var clientIp = TryGetClientIp(context);

            // ————————————————— 2.  Allow-list check  ———————————————————
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return SmtpResponse.AuthenticationRequired;
            }

            _log.LogInformation("Incoming relay request from {IP}", clientIp);

            // ————————————————— 3.  Re-hydrate MimeMessage —————————————
            using var ms = new MemoryStream();
            foreach (var seg in buffer)
                ms.Write(seg.Span);
            ms.Position = 0;

            var message = MimeMessage.Load(ms);

            // ————————————————— 4.  Prepare protocol log ——————————————
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);

            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");

            // ————————————————— 5.  Outbound SMTP w/ trace ————————————
            using var smtp = new SmtpClient(new MailKit.ProtocolLogger(protoPath, append: true));

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
                string.Join(",", message.From),
                string.Join(",", message.To));

            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            _log.LogInformation("Smarthost relay complete");
            _log.LogInformation("Relayed mail from {IP}", clientIp);

            return SmtpResponse.Ok;
        }

        // —— Helper: robustly pull the remote IP from the session context ——
        private static string TryGetClientIp(ISessionContext ctx)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // a) Look for a RemoteEndPoint/RemoteEndpoint property
            foreach (var name in new[] { "RemoteEndPoint", "RemoteEndpoint" })
            {
                var p = ctx.GetType().GetProperty(name, BF);
                if (p?.GetValue(ctx) is EndPoint ep &&
                    ep is IPEndPoint ipEp)
                    return ipEp.Address.ToString();
            }

            // b) Fall back to the Properties bag (Dictionary / PropertyBag)
            var propsProp = ctx.GetType().GetProperty("Properties", BF);
            if (propsProp?.GetValue(ctx) is IEnumerable bag)
            {
                foreach (var entry in bag)
                {
                    if (entry is IPEndPoint ipEp)
                        return ipEp.Address.ToString();

                    // KeyValuePair<string, object> or similar
                    var valueProp = entry.GetType().GetProperty("Value");
                    if (valueProp?.GetValue(entry) is IPEndPoint ip2)
                        return ip2.Address.ToString();
                }
            }

            return "unknown";
        }
    }
}
