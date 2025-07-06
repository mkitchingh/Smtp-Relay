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
    public static class MailSender
    {
        // Called from Worker.RelayStore.SaveAsync(...)
        public static async Task SendAsync(Config cfg, ReadOnlySequence<byte> buffer, CancellationToken ct)
        {
            // Ensure log folder exists and build our protocol-log path
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            // We'll write protocol traffic to smtp-YYYYMMDD.log
            var protoLogPath = Path.Combine(
                logDir,
                $"smtp-{DateTime.Now:yyyyMMdd}.log");

            // MailKit client with protocol logger
            using var protocolLogger = new ProtocolLogger(protoLogPath, append: true);
            using var client = new SmtpClient(protocolLogger);

            try
            {
                // parse the incoming message
                var data = buffer.ToArray();
                var message = MimeMessage.Load(new MemoryStream(data));

                Log.Information("Connecting to {Host}:{Port} (STARTTLS={UseTls})",
                    cfg.SmartHost, cfg.SmartHostPort, cfg.UseStartTls);

                await client.ConnectAsync(
                    cfg.SmartHost,
                    cfg.SmartHostPort,
                    cfg.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                    ct);

                if (!string.IsNullOrEmpty(cfg.Username))
                {
                    Log.Information("Authenticating as {Username}", cfg.Username);
                    await client.AuthenticateAsync(cfg.Username, cfg.Password, ct);
                }

                Log.Information("Sending message from {From} to {To}",
                    message.From, message.To);

                await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);

                Log.Information("Smarthost relay complete");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Relay failure");
                throw;
            }
        }
    }
}
