using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace SmtpRelay
{
    public class MailSender
    {
        private readonly ILogger<MailSender> _logger;

        public MailSender(ILogger<MailSender> logger)
        {
            _logger = logger;
        }

        public async Task SendAsync(MimeMessage message, Config cfg, CancellationToken ct = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            var mode = cfg.GetEffectiveSecurity();
            var options = mode switch
            {
                OutboundSecurityMode.Smtps => SecureSocketOptions.SslOnConnect, // SMTPS (465)
                OutboundSecurityMode.StartTls => SecureSocketOptions.StartTls,  // STARTTLS (587)
                _ => SecureSocketOptions.None
            };

            _logger.LogInformation("Connecting to {Host}:{Port} (Security={Security}, SocketOptions={Options})",
                cfg.SmartHost, cfg.SmartHostPort, mode, options);

            using var client = new SmtpClient();

            // Reasonable timeouts; avoid hanging forever
            client.Timeout = 15000;

            await client.ConnectAsync(cfg.SmartHost, cfg.SmartHostPort, options, ct).ConfigureAwait(false);

            // Authenticate only if username provided (supports unauthenticated relays)
            if (!string.IsNullOrWhiteSpace(cfg.Username))
            {
                await client.AuthenticateAsync(cfg.Username, cfg.Password ?? string.Empty, ct).ConfigureAwait(false);
            }

            await client.SendAsync(message, ct).ConfigureAwait(false);
            await client.DisconnectAsync(true, ct).ConfigureAwait(false);
        }
    }
}