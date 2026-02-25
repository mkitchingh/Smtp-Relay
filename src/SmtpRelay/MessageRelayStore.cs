using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Serilog;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpRelay
{
    public class MessageRelayStore : IMessageStore
    {
        private readonly Config _cfg;
        private readonly Serilog.ILogger _log;

        public MessageRelayStore(Config cfg, Serilog.ILogger log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<SmtpResponse> SaveAsync(
            ISessionContext context,
            IMessageTransaction transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = buffer.ToArray();
                var message = MimeMessage.Load(new MemoryStream(data));

                var mode = _cfg.GetEffectiveSecurity();

                var socketOptions = mode switch
                {
                    OutboundSecurityMode.Smtps    => SecureSocketOptions.SslOnConnect, // SMTPS / 465
                    OutboundSecurityMode.StartTls => SecureSocketOptions.StartTls,     // STARTTLS / 587
                    _                             => SecureSocketOptions.None         // None / 25 (or whatever port)
                };

                _log.Information(
                    "Connecting to {Host}:{Port} (Security={Security}, SocketOptions={Options})",
                    _cfg.SmartHost,
                    _cfg.SmartHostPort,
                    mode,
                    socketOptions);

                using var client = new SmtpClient();

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

                return SmtpResponse.Ok;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Relay failure from {Remote}", context.RemoteEndPoint);
                return SmtpResponse.TransactionFailed;
            }
        }
    }
}