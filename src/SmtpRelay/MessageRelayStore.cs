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
using SmtpResponse = SmtpServer.Protocol.SmtpResponse;

namespace SmtpRelay
{
    public sealed class MessageRelayStore : MessageStore
    {
        private readonly Config  _cfg;
        private readonly ILogger _log;

        public MessageRelayStore(Config cfg, ILogger logger)
        {
            _cfg = cfg;
            _log = logger;
        }

        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext        context,
            IMessageTransaction    transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken      cancel)
        {
            // 1️⃣ Extract & normalise remote IP
            var clientIp = NormaliseClientIp(TryGetClientIp(context));

            // 2️⃣ Allow-list check
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return SmtpResponse.AuthenticationRequired;
            }

            _log.LogInformation("Incoming relay request from {IP}", clientIp);

            // 3️⃣ Re-hydrate MimeMessage
            using var ms = new MemoryStream();
            foreach (var seg in buffer)
                ms.Write(seg.Span);
            ms.Position = 0;
            var message = MimeMessage.Load(ms);

            // 4️⃣ Prepare protocol log
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);

            var protoPath = Path.Combine(
                logDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");   // local date

            // **Ensure file exists and note the path**
            if (!File.Exists(protoPath))
                File.WriteAllText(protoPath, string.Empty);
            _log.LogInformation("Protocol trace file: {Path}", protoPath);

            using var protoStream = new FileStream(
                protoPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var smtpLogger  = new MailKit.ProtocolLogger(protoStream, leaveOpen: false);

            // 5️⃣ Outbound SMTP with trace
            using var smtp = new SmtpClient(smtpLogger);

            _log.LogInformation("Connecting to {Host}:{Port} (STARTTLS={TLS})",
                _cfg.SmartHost, _cfg.SmartHostPort, _cfg.UseStartTls);

            await smtp.ConnectAsync(
                _cfg.SmartHost,
                _cfg.SmartHostPort,
                _cfg.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable
                                 : SecureSocketOptions.None,
                cancel);

            if (!string.IsNullOrWhiteSpace(_cfg.Username))
            {
                _log.LogInformation("Authenticating as {User}", _cfg.Username);
                await smtp.AuthenticateAsync(_cfg.Username, _cfg.Password, cancel);
            }

            _log.LogInformation("Sending message from {From} to {To}",
                string.Join(",", message.From), string.Join(",", message.To));

            await smtp.SendAsync(message, cancel);
            await smtp.DisconnectAsync(true, cancel);

            // Flush to guarantee bytes on disk
            protoStream.Flush(true);

            _log.LogInformation("Smarthost relay complete");
            _log.LogInformation("Relayed mail from {IP}", clientIp);

            return SmtpResponse.Ok;
        }

        // ——— Helpers ———————————————————————————————————————————————
        private static string TryGetClientIp(ISessionContext ctx)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in new[] { "RemoteEndPoint", "RemoteEndpoint" })
            {
                var p = ctx.GetType().GetProperty(name, BF);
                if (p?.GetValue(ctx) is IPEndPoint ipEp)
                    return ipEp.Address.ToString();
            }

            var propsProp = ctx.GetType().GetProperty("Properties", BF);
            if (propsProp?.GetValue(ctx) is IEnumerable bag)
            {
                foreach (var entry in bag)
                {
                    if (entry is IPEndPoint ip1) return ip1.Address.ToString();
                    var valProp = entry.GetType().GetProperty("Value");
                    if (valProp?.GetValue(entry) is IPEndPoint ip2)
                        return ip2.Address.ToString();
                }
            }
            return "unknown";
        }

        private static string NormaliseClientIp(string ip)
        {
            if (!IPAddress.TryParse(ip, out var addr)) return ip;
            if (addr.Equals(IPAddress.Any) || addr.Equals(IPAddress.IPv6Any)) return "127.0.0.1";
            if (IPAddress.IsLoopback(addr)) return "127.0.0.1";
            return ip;
        }
    }
}
