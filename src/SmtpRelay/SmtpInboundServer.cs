using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using Microsoft.Extensions.Logging;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpRelay
{
    public sealed class MessageRelayStore : IMessageStore
    {
        private readonly Config  _cfg;
        private readonly ILogger _log;

        public MessageRelayStore(Config cfg, ILogger log)
        {
            _cfg = cfg;
            _log = log;
        }

        // NOTE: fully qualified return type to avoid ambiguity
        public async Task<SmtpServer.Protocol.SmtpResponse> SaveAsync(
            ISessionContext context,
            IMessageTransaction transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            // copy buffer to stream
            await using var ms = new MemoryStream();
            foreach (var segment in buffer)
                await ms.WriteAsync(segment, cancellationToken);
            ms.Position = 0;

            var message = await MimeMessage.LoadAsync(ms, cancellationToken);

            // SMTP dialog log
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);
            var protoPath = Path.Combine(logDir,
                $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");

            try
            {
                using var client = new SmtpClient(new ProtocolLogger(protoPath));

                await client.ConnectAsync(
                    _cfg.SmartHost,
                    _cfg.SmartHostPort,
                    _cfg.UseStartTls
                        ? MailKit.Security.SecureSocketOptions.StartTls
                        : MailKit.Security.SecureSocketOptions.None,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(_cfg.Username))
                    await client.AuthenticateAsync(_cfg.Username, _cfg.Password, cancellationToken);

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _log.LogInformation("Relayed {subject}", message.Subject);
                return SmtpServer.Protocol.SmtpResponse.Ok;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Relay failed");
                return new SmtpServer.Protocol.SmtpResponse(
                    SmtpServer.Protocol.SmtpReplyCode.TransactionFailed);
            }
        }
    }
}
