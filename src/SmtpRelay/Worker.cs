using System;
using System.IO;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpRelay
{
    public class Worker : BackgroundService
    {
        readonly Config _cfg;
        readonly ILogger _log;

        public Worker(Config cfg, ILogger log)
        {
            _cfg = cfg;
            _log = log;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // same log folder
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            // single SMTP log file for full protocol + your "relayed mail" info
            var smtpLogFile = Path.Combine(logDir, $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");

            // build the SMTP-server, injecting your MessageRelayStore
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Endpoint(builder => builder
                    .Port(25)            // default port 25
                    .AllowUnsecure()     // relay unencrypted
                    .Build())
                .MessageStore(new MessageRelayStore(_cfg, _log))
                .ProtocolLogger(new ProtocolLogger(smtpLogFile, append: true))
                .Build();

            var server = new SmtpServer.SmtpServer(options);

            _log.Information("SMTP Relay starting on port 25");
            return server.StartAsync(stoppingToken);
        }
    }
}
