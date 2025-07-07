using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtpServer;
using SmtpServer.Storage;

namespace SmtpRelay
{
    public class Worker : BackgroundService
    {
        private readonly Config _cfg;
        private readonly ILogger _log;
        private readonly IMessageStore _store;

        public Worker(Config cfg)
        {
            _cfg   = cfg;
            _log   = Log.Logger;
            _store = new MessageRelayStore(_cfg, _log);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // build SMTP server options (no ProtocolLogger here!)
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(_cfg.ListenPort, allowUnsecure: false)
                .MessageStore(_store)
                .Build();

            // create and start server
            var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider: null);

            _log.Information("SMTP Relay listening on port {Port}", _cfg.ListenPort);
            await smtpServer.StartAsync(stoppingToken);
        }
    }
}
