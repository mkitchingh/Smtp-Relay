using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SmtpServer;                        // SmtpServerOptionsBuilder
using SmtpServer.ComponentModel;
using SmtpServer.Storage;

namespace SmtpRelay
{
    /// <summary>Windows-service host that listens on port 25 and
    /// relays accepted mail to the configured smart-host.</summary>
    public sealed class Worker : BackgroundService
    {
        private readonly Config          _config;
        private readonly Serilog.ILogger _log;     // explicit Serilog type

        public Worker()
        {
            _config = Config.Load();
            _log    = Log.Logger.ForContext<Worker>();
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            _log.Information("Starting SMTP listener on port 25");

            // ── relay-mode banner (matches legacy log) ────────────────────
            if (_config.AllowAllIPs)
                _log.Information("Relay mode: Allow ALL IPs");
            else
                _log.Information("Relay mode: Allow {Count} range(s)",
                                 _config.AllowedIPs.Count);

            // ── ensure %ProgramData%\SMTP Relay\logs exists ───────────────
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);

            // ── build SmtpServer options ──────────────────────────────────
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(25)
                .Build();

            // ── bridge Serilog → Microsoft.Extensions.Logging ────────────
            using var loggerFactory = LoggerFactory.Create(b => b.AddSerilog());
            Microsoft.Extensions.Logging.ILogger msLogger =
                loggerFactory.CreateLogger<MessageRelayStore>();

            // ── register our message store ────────────────────────────────
            var services = new ServiceProvider();
            services.Add(new MessageRelayStore(_config, msLogger));

            var server = new SmtpServer.SmtpServer(options, services);

            await server.StartAsync(token);         // run until service stops

            _log.Information("SMTP listener stopped");
        }
    }
}
