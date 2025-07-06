// src/SmtpRelay/Worker.cs
using System;
using System.IO;
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
        readonly Config  _cfg;
        readonly ILogger _log;

        public Worker()
        {
            _cfg = Config.Load();
            _log = Log.Logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Build up our paths
            var date   = DateTime.Now.ToString("yyyyMMdd");
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            // This is your plain SMTP log file
            var smtpLogPath = Path.Combine(logDir, $"smtp-{date}.log");

            // SmtpServer options
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(_cfg.ListenPort, allowUnsecure: true)
                .MessageStore(new MessageRelayStore(_cfg, _log))
                // <-- write the inbound SMTP dialog into the same smtp-YYYYMMDD.log
                .ProtocolLogger(new ProtocolLogger(smtpLogPath, append: true))
                .Build();

            _log.Information("Starting inbound SMTP listener on port {Port}", _cfg.ListenPort);

            // Start the server
            var server = new SmtpServer.SmtpServer(options);
            return server.StartAsync(stoppingToken);
        }
    }
}
