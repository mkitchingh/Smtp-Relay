using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Serilog;

namespace SmtpRelay
{
    /// <summary>
    /// Handles relaying an incoming SMTP message buffer to the smart host.
    /// </summary>
    public static class MailSender
    {
        /// <summary>
        /// Sends the message contained in <paramref name="buffer"/> via the configured smart host.
        /// </summary>
        public static async Task SendAsync(Config cfg, ReadOnlySequence<byte> buffer, CancellationToken ct)
        {
            // Load the incoming message
            var raw = buffer.ToArray();
            var message = MimeMessage.Load(new MemoryStream(raw));

            // Log that we're attempting to connect
            Log.Information(
                "Connecting to smarthost {Host}:{Port} (STARTTLS={Tls})",
                cfg.SmartHost, cfg.SmartHostPort, cfg.UseStartTls);

            using var client = new SmtpClient();
            await client.ConnectAsync(
                cfg.SmartHost,
                cfg.SmartHostPort,
                cfg.UseStartTls
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.Auto,
                ct);

            if (!string.IsNullOrEmpty(cfg.Username))
            {
                await client.AuthenticateAsync(cfg.Username, cfg.Password, ct);
                Log.Information("Authenticated as {User}", cfg.Username);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            Log.Information("Message relayed successfully");
        }
    }
}
