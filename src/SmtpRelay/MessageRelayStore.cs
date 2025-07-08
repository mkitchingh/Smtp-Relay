/* ──────────────────────────────────────────────────────────────
 * MessageRelayStore.cs  –  tested for .NET 8 + MailKit 4.11.0
 * Logs folder: C:\Program Files\SMTP Relay\service\logs\
 *  - app-YYYYMMDD.log  (Serilog events)
 *  - smtp-YYYYMMDD.log (protocol commands / replies only)
 * DATA block suppressed; allow-list respected.
 * ────────────────────────────────────────────────────────────── */
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

            // rebuild MimeMessage
            using var ms = new MemoryStream();
            foreach (var seg in buf) ms.Write(seg.Span);
            ms.Position = 0;
            var message = MimeMessage.Load(ms);

            // logs beside service EXE
            var logDir   = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(protoPath)) File.WriteAllText(protoPath, string.Empty);
            _log.LogInformation("Protocol trace file ready: {Path}", protoPath);

            // SMTP client with minimal protocol logger
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

            _log.LogInformation("Sending message from {From} to {To}",
                string.Join(",", message.From), string.Join(",", message.To));

            await smtp.SendAsync(message, cancel);
            await smtp.
