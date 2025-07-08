using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Options;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpRelay
{
    /// <summary>
    /// Hosts the in-process SMTP server and relays accepted messages to the smart host.
    /// </summary>
    public sealed class Worker : BackgroundService
    {
        private readonly Config _config;
        private readonly ILogger _log;

        public Worker()
        {
            _config = Config.Load();
            _log    = Log.Logger.ForContext<Worker>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information("Starting SMTP listener on port 25");

            // ── Protocol (wire-level) log ─────────────────────────────
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");
            Directory.CreateDirectory(logDir);

            var protoPath = Path.Combine(logDir, $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");

            // ── Configure SmtpServer ──────────────────────────────────
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(25)                                 // listen on port 25
                .Build();

            var services = new ServiceProvider();
            services.Add(new MessageRelayStore(_config));

            var server = new SmtpServer.SmtpServer(
                options,
                services,
                new ProtocolLogger(protoPath));           // ← writes smtp-YYYYMMDD.log

            // ── Run until the service is stopped ─────────────────────
            await server.StartAsync(stoppingToken);

            _log.Information("SMTP listener stopped");
        }
    }
}
