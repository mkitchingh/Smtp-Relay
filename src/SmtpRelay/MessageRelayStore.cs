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
using MailKit;
using MimeKit;
using NetTools;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using SmtpResponse = SmtpServer.Protocol.SmtpResponse;

namespace SmtpRelay
{
    /// <summary>
    /// Relays accepted messages to the configured smart-host, logging only
    /// protocol commands/responses (no DATA contents).
    /// </summary>
    public sealed class MessageRelayStore : MessageStore
    {
        private readonly Config  _cfg;
        private readonly ILogger _log;
        public MessageRelayStore(Config cfg, ILogger log) { _cfg = cfg; _log = log; }

        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext        ctx,
            IMessageTransaction    txn,
            ReadOnlySequence<byte> buf,
            CancellationToken      cancel)
        {
            var clientIp = Normalise(TryGetClientIp(ctx));

            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return SmtpResponse.AuthenticationRequired;
            }
            _log.LogInformation("Incoming relay request from {IP}", clientIp);

            // ── Re-hydrate MimeMessage ─────────────────────────────────
            using var ms = new MemoryStream();
            foreach (var seg in buf) ms.Write(seg.Span);
            ms.Position = 0;
            var message = MimeMessage.Load(ms);

            // ── Log folder beside service EXE ──────────────────────────
            var logDir   = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(protoPath)) File.WriteAllText(protoPath, string.Empty);

            _log.LogInformation("Protocol trace file ready: {Path}", protoPath);

            // ── SMTP client with custom MinimalProtocolLogger ──────────
            using var smtp = new SmtpClient(new MinimalProtocolLogger(protoPath));

            _log.LogInformation("Connecting to {Host}:{Port} (STARTTLS={TLS})",
                _cfg.SmartHost, _cfg.SmartHostPort, _cfg.UseStartTls);

            await smtp.ConnectAsync(
                _cfg.SmartHost, _cfg.SmartHostPort,
                _cfg.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
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

            _log.LogInformation("Smarthost relay complete");
            _log.LogInformation("Relayed mail from {IP}", clientIp);

            return SmtpResponse.Ok;
        }

        // ───── minimal protocol logger (skips DATA block) ─────────────
        private sealed class MinimalProtocolLogger : IProtocolLogger, IDisposable
        {
            private readonly StreamWriter _sw;
            private bool _inData;

            public MinimalProtocolLogger(string path)
            {
                _sw = new StreamWriter(new FileStream(
                    path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
            }

            public void LogConnect(Uri uri) =>
                _sw.WriteLine($"[{DateTime.Now:HH:mm:ss}] CONNECT {uri}");

            public void LogClient(byte[] buffer, int offset, int count)
            {
                var line = System.Text.Encoding.ASCII.GetString(buffer, offset, count).TrimEnd();
                if (_inData)
                {
                    if (line == ".") { _inData = false; _sw.WriteLine("C: <DATA END>"); }
                    return;
                }

                _sw.WriteLine("C: " + line);
                if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    _inData = true;
            }

            public void LogServer(byte[] buffer, int offset, int count)
            {
                var line = System.Text.Encoding.ASCII.GetString(buffer, offset, count).TrimEnd();
                _sw.WriteLine("S: " + line);
            }

            public void Dispose() { _sw.Flush(); _sw.Dispose(); }
        }

        // ───── Helpers ────────────────────────────────────────────────
        static string TryGetClientIp(ISessionContext ctx)
        {
            const BindingFlags BF = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            foreach (var n in new[] { "RemoteEndPoint", "RemoteEndpoint" })
                if (ctx.GetType().GetProperty(n, BF)?.GetValue(ctx) is IPEndPoint ep)
                    return ep.Address.ToString();

            var bag = ctx.GetType().GetProperty("Properties", BF)?.GetValue(ctx) as IEnumerable;
            if (bag != null)
                foreach (var e in bag)
                {
                    if (e is IPEndPoint ep) return ep.Address.ToString();
                    var v = e.GetType().GetProperty("Value")?.GetValue(e) as IPEndPoint;
                    if (v != null) return v.Address.ToString();
                }
            return "unknown";
        }
        static string Normalise(string ip)
        {
            if (!IPAddress.TryParse(ip, out var a)) return ip;
            if (a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any)) return "127.0.0.1";
            if (IPAddress.IsLoopback(a)) return "127.0.0.1";
            return ip;
        }
    }
}
