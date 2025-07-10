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
using MailKit.Net.Smtp;
using MailKit;
using MailKit.Security;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

/* disambiguate SmtpResponse */
using SmtpSrvResponse = SmtpServer.Protocol.SmtpResponse;

namespace SmtpRelay
{
    public sealed class MessageRelayStore : MessageStore
    {
        private readonly Config  _cfg;
        private readonly ILogger _log;

        public MessageRelayStore(Config cfg, ILogger log)
        {
            _cfg = cfg;
            _log = log;
        }

        public override async Task<SmtpSrvResponse> SaveAsync(
            ISessionContext        ctx,
            IMessageTransaction    txn,
            ReadOnlySequence<byte> buf,
            CancellationToken      cancel)
        {
            string clientIp = GetClientIp(ctx) ?? "unknown";

            /* ── relay restriction check ─────────────────────── */
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return new SmtpSrvResponse(SmtpReplyCode.MailboxUnavailable, "Relay access denied");
            }

            /* ── rebuild MimeMessage ──────────────────────────── */
            using var ms = new MemoryStream();
            foreach (var seg in buf) ms.Write(seg.Span);
            ms.Position = 0;
            var message = MimeMessage.Load(ms);

            /* ── protocol logger path ─────────────────────────── */
            Directory.CreateDirectory(Config.SharedLogDir);
            var protoPath = Path.Combine(Config.SharedLogDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");

            using var smtp = new SmtpClient(new MinimalProtocolLogger(protoPath));

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

            /* ── add delimiter after each conversation ────────── */
            File.AppendAllText(protoPath,
                Environment.NewLine + "-------------------------------------" + Environment.NewLine);

            return SmtpSrvResponse.Ok;
        }

        /* ========== minimal protocol logger (unchanged) ========== */
        private sealed class MinimalProtocolLogger : IProtocolLogger, IDisposable
        {
            private readonly StreamWriter _sw;
            private bool _inData;

            public MinimalProtocolLogger(string path) =>
                _sw = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

            public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }
                = new DummyDetector();

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

            private sealed class DummyDetector : IAuthenticationSecretDetector
            {
                public bool IsSecret(string text) => false;
                public IList<AuthenticationSecret> DetectSecrets(byte[] b, int o, int c)
                    => new List<AuthenticationSecret>();
            }
        }

        /* ───────── robust client-IP extractor ───────── */
        private static string? GetClientIp(ISessionContext ctx)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            /* 1) direct property on ISessionContext implementation */
            foreach (var p in ctx.GetType().GetProperties(BF))
                if (typeof(EndPoint).IsAssignableFrom(p.PropertyType) &&
                    p.GetValue(ctx) is IPEndPoint epProp)
                    return epProp.Address.ToString();

            /* 2) look for known keys in Properties bag */
            if (ctx.Properties.TryGetValue("RemoteEndPoint", out var o1) && o1 is IPEndPoint ep1)
                return ep1.Address.ToString();
            if (ctx.Properties.TryGetValue("SessionRemoteEndPoint", out var o2) && o2 is IPEndPoint ep2)
                return ep2.Address.ToString();

            /* 3) any IPEndPoint or parsable IP in bag values */
            foreach (var v in ctx.Properties.Values)
            {
                switch (v)
                {
                    case IPEndPoint ipEp:
                        return ipEp.Address.ToString();
                    case EndPoint ep when ep is IPEndPoint ipep:
                        return ipep.Address.ToString();
                    case string s when IPAddress.TryParse(s, out var ip):
                        return ip.ToString();
                }
            }
            return null;
        }
    }
}
