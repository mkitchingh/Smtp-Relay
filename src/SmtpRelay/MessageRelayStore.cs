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
                // Convert the SMTPServer buffer into a stream (safe, standard pattern)
                await using var stream = new MemoryStream();
                var position = buffer.GetPosition(0);

                while (buffer.TryGet(ref position, out var memory))
                {
                    await stream.WriteAsync(memory, cancellationToken);
                }

                stream.Position = 0;

                // Parse message
                var message = await MimeMessage.LoadAsync(stream, cancellationToken);

                // Determine outbound security mode (backward compatible)
                var mode = _cfg.GetEffectiveSecurity();

                var socketOptions = mode switch
                {
                    OutboundSecurityMode.Smtps    => SecureSocketOptions.SslOnConnect, // SMTPS / 465
                    OutboundSecurityMode.StartTls => SecureSocketOptions.StartTls,     // STARTTLS / 587
                    _                             => SecureSocketOptions.None
                };

                _log.LogInformation(
                    "Connecting to {Host}:{Port} (Security={Security}, SocketOptions={Options})",
                    _cfg.SmartHost,
                    _cfg.SmartHostPort,
                    mode,
                    socketOptions);

                // Restore MailKit protocol logging (smtp-YYYYMMDD.log) when logging is enabled
                MailKit.ProtocolLogger? protocolLogger = null;
                if (_cfg.EnableLogging)
                {
                    Directory.CreateDirectory(Config.SharedLogDir);
                    var smtpLogPath = Path.Combine(Config.SharedLogDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");
                    protocolLogger = new MailKit.ProtocolLogger(smtpLogPath, true);
                }

                using (protocolLogger)
                using var client = protocolLogger != null
                    ? new SmtpClient(protocolLogger)
                    : new SmtpClient();

                client.Timeout = 15000;

                await client.ConnectAsync(
                    _cfg.SmartHost,
                    _cfg.SmartHostPort,
                    socketOptions,
                    cancellationToken);

                // Authenticate only when a username is provided
                if (!string.IsNullOrWhiteSpace(_cfg.Username))
                {
                    await client.AuthenticateAsync(
                        _cfg.Username,
                        _cfg.Password ?? string.Empty,
                        cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                return SmtpResponse.Ok;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Relay failure");
                return SmtpResponse.TransactionFailed;
            }
        }
    }
}