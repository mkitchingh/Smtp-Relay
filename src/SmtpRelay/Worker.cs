using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtpServer;                       // SmtpServerOptionsBuilder lives here
using SmtpServer.ComponentModel;
using SmtpServer.Storage;

namespace SmtpRelay
{
    /// <summary>
    /// Hosts an SMTP listener on port 25 and relays accepted mail to the smart host.
    /// </summary>
    public sealed class Worker : BackgroundService
    {
        private readonly Config  _config;
        private readonly ILogger _log;

        public Worker()
        {
            _config = Config.Load();
            _log    = Log.Logger.ForContext<Worker>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information("Starting SMTP listener on port 25");

            // ── Ensure %ProgramData%\SMTP Relay\logs exists ────────────────
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);

            // ── Configure SmtpServer library ───────────────────────────────
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(25)                                     // listen on TCP 25
                .Build();

            var services = new ServiceProvider();
            services.Add(new MessageRelayStore(_config, _log)); // ← pass logger

            var server = new SmtpServer.SmtpServer(options, services);

            // ── Run until the Windows service stops ───────────────────────
            await server.StartAsync(stoppingToken);

            _log.Information("SMTP listener stopped");
        }
    }
}
