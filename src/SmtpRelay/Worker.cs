using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SmtpServer;
using SmtpServer.ComponentModel;

namespace SmtpRelay
{
    public sealed class Worker : BackgroundService
    {
        private readonly Config          _cfg;
        private readonly Serilog.ILogger _log;

        public Worker()
        {
            _cfg = Config.Load();
            _log = SmtpLogger.Initialise(_cfg).ForContext<Worker>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Ensure the ONE shared log folder exists; nothing in ProgramData
            System.IO.Directory.CreateDirectory(Config.SharedLogDir);

            _log.Information("Starting SMTP listener on port 25");
            _log.Information(_cfg.AllowAllIPs
                ? "Relay mode: Allow ALL IPs"
                : "Relay mode: Allow {Ranges} range(s)", _cfg.AllowedIPs.Count);

            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(25)
                .Build();

            var provider = new ServiceProvider();
            provider.Add(new MessageRelayStore(_cfg,
                LoggerFactory.Create(b => b.AddSerilog()).CreateLogger<MessageRelayStore>()));

            var server = new SmtpServer.SmtpServer(options, provider);
            await server.StartAsync(stoppingToken);
            _log.Information("SMTP listener stopped");
        }
    }
}
