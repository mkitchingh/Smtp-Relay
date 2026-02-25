using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using SmtpResponse = SmtpServer.Protocol.SmtpResponse;

namespace SmtpRelay
{
    public class MessageRelayStore : IMessageStore
    {
        private readonly Config _cfg;
        private readonly ILogger<MessageRelayStore> _log;

        public MessageRelayStore(Config cfg, ILogger<MessageRelayStore> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<SmtpResponse> SaveAsync(
            ISessionContext context,
            IMessageTransaction transaction,
            System.Buffers.ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = new MemoryStream();
                var position = buffer.GetPosition(0);

                while (buffer.TryGet(ref position, out var memory))
                {
                    await stream.WriteAsync(memory, cancellationToken);
                }

                stream.Position = 0;

                var message = await MimeMessage.LoadAsync(stream, cancellationToken);

                var mode = _cfg.GetEffectiveSecurity();

                var socketOptions = mode switch
                {
                    OutboundSecurityMode.Smtps    => SecureSocketOptions.SslOnConnect,
                    OutboundSecurityMode.StartTls => SecureSocketOptions.StartTls,
                    _                             => SecureSocketOptions.None
                };

                _log.LogInformation(
                    "Connecting to {Host}:{Port} (Security={Security}, SocketOptions={Options})",
                    _cfg.SmartHost,
                    _cfg.SmartHostPort,
                    mode,
                    socketOptions);

                if (_cfg.EnableLogging)
                {
                    Directory.CreateDirectory(Config.SharedLogDir);
                    var smtpLogPath = Path.Combine(Config.SharedLogDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");

                    using var protocolLogger = new RedactingSmtpProtocolLogger(smtpLogPath, append: true);
                    using var client = new SmtpClient(protocolLogger) { Timeout = 15000 };

                    await SendWithClientAsync(client, message, socketOptions, cancellationToken);
                }
                else
                {
                    using var client = new SmtpClient { Timeout = 15000 };
                    await SendWithClientAsync(client, message, socketOptions, cancellationToken);
                }

                return SmtpResponse.Ok;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Relay failure");
                return SmtpResponse.TransactionFailed;
            }
        }

        private async Task SendWithClientAsync(
            SmtpClient client,
            MimeMessage message,
            SecureSocketOptions socketOptions,
            CancellationToken cancellationToken)
        {
            await client.ConnectAsync(
                _cfg.SmartHost,
                _cfg.SmartHostPort,
                socketOptions,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_cfg.Username))
            {
                await client.AuthenticateAsync(
                    _cfg.Username,
                    _cfg.Password ?? string.Empty,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}