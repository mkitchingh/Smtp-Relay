using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // shared log directory (same one you use in Program.cs)
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");

            // build the server options, *including* the protocol logger
            var smtpLogFile = Path.Combine(logDir, $"smtp-{DateTime.UtcNow:yyyyMMdd}.log");
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Endpoint(builder => builder
                    .Port(_cfg.ListenPort)
                    .AllowUnsecure()
                    .Build())
                .MessageStore(new MessageRelayStore(_cfg, _log))
                // ← this is the only new bit: log *every* SMTP command/response
                .ProtocolLogger(new ProtocolLogger(smtpLogFile, append: true))
                .Build();

            var serviceProvider = new ServiceProviderBuilder()
                .UseLoggerFactory(new LoggerFactoryAdapter(_log))
                .Build();

            var server = new SmtpServer.SmtpServer(options, serviceProvider);

            _log.Information("SMTP Relay starting on port {Port}", _cfg.ListenPort);
            await server.StartAsync(stoppingToken);
        }
    }
}
