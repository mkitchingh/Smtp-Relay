using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;                       // ← IPEndPoint
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
            /* ---------- client IP (works on all SmtpServer builds) ---------- */
            string clientIp = "unknown";
            if (ctx.Properties.TryGet("SessionRemoteEndPoint", out var o) &&
                o is IPEndPoint ep)
                clientIp = ep.Address.ToString();

            /* ---------- relay restriction check ---------- */
            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return new SmtpSrvResponse(SmtpReplyCode.MailboxUnavailable,
                                           "Relay access denied");
            }

            /* ---------- rebuild MimeMessage ---------- */
            using var ms = new MemoryStream();
            foreach (var seg in buf) ms.Write(seg.Span);
            ms.Position = 0;
            var message = MimeMessage.Load(ms);

            /* ---------- protocol log path ---------- */
            Directory.CreateDirectory(Config.SharedLogDir);
            var protoPath = Path.Combine(
                Config.SharedLogDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");

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

            /* ---------- delimiter after each conversation ---------- */
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
                    => new List<AuthenticationSecret>();   // empty list satisfies interface
            }
        }
    }
}
