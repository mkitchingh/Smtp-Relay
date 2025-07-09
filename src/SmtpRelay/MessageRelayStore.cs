/* MessageRelayStore.cs – unified log folder + 550 reject code */
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MailKit;
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
        public MessageRelayStore(Config cfg, ILogger log) { _cfg = cfg; _log = log; }

        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext        ctx,
            IMessageTransaction    txn,
            ReadOnlySequence<byte> buf,
            CancellationToken      cancel)
        {
            var clientIp = Normalise(TryGetClientIp(ctx));

            /* ── allow-list check ─────────────────────────────── */
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);

                // 550 Relay Access Denied
                return new SmtpResponse(SmtpReplyCode.MailboxUnavailable,
                                        "Relay access denied");
            }

            /* ── rebuild MimeMessage ──────────────────────────── */
            using var ms = new MemoryStream();
            foreach (var seg in buf) ms.Write(seg.Span);
            ms.Position = 0;
            var message = MimeMessage.Load(ms);

            /* ── shared log folder ────────────────────────────── */
            var logDir   = Config.SharedLogDir;
            Directory.CreateDirectory(logDir);
            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");

            using var smtp = new SmtpClient(new MinimalProtocolLogger(protoPath));

            /* ── send via smart-host ──────────────────────────── */
            _log.LogInformation("Connecting to {Host}:{Port} (STARTTLS={TLS})",
                _cfg.SmartHost, _cfg.SmartHostPort, _cfg.UseStartTls);

            await smtp.ConnectAsync(
                _cfg.SmartHost, _cfg.SmartHostPort,
                _cfg.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable
                                 : SecureSocketOptions.None,
                cancel);

            if (!string.IsNullOrWhiteSpace(_cfg.Username))
            {
                _log.LogInformation("Authenticating as {User}", _cfg.Username);
                await smtp.AuthenticateAsync(_cfg.Username, _cfg.Password, cancel);
            }

            await smtp.SendAsync(message, cancel);
            await smtp.DisconnectAsync(true, cancel);

            _log.LogInformation("Relayed mail from {IP}", clientIp);
            PurgeOldSmtpLogs(logDir, _cfg.RetentionDays);
            return SmtpResponse.Ok;
        }

        /* ───── minimal protocol logger (DATA suppressed) ───── */
        private sealed class MinimalProtocolLogger : IProtocolLogger, IDisposable
        {
            private readonly StreamWriter _sw;
            private bool _inData;
            public MinimalProtocolLogger(string path) =>
                _sw = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

            public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }
                = new NoSecretDetector();

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
            public void LogServer(byte[] buffer, int offset, int count) =>
                _sw.WriteLine("S: " + System.Text.Encoding.ASCII.GetString(buffer, offset, count).TrimEnd());
            public void Dispose() { _sw.Flush(); _sw.Dispose(); }

            private sealed class NoSecretDetector : IAuthenticationSecretDetector
            {
                public bool IsSecret(string text) => false;
                public IList<AuthenticationSecret> DetectSecrets(byte[] buffer, int o, int c)
                    => Array.Empty<AuthenticationSecret>();
            }
        }

        /* ───── helpers ─────────────────────────────────────── */
        static void PurgeOldSmtpLogs(string dir, int keepDays)
        {
            try
            {
                foreach (var f in Directory.GetFiles(dir, "smtp-*.log"))
                    if (File.GetCreationTimeUtc(f) < DateTime.UtcNow.AddDays(-keepDays))
                        File.Delete(f);
            } catch { /* ignore */ }
        }

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
        static string Normalise(string ip) =>
            IPAddress.TryParse(ip, out var a) && (IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any))
                ? "127.0.0.1" : ip;
    }
}
