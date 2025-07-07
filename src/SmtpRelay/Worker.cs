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

        public Worker(Config cfg)
        {
            _cfg = cfg;
            _log = Log.ForContext<Worker>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(_cfg.ListenPort)
                .MessageStore(new MessageRelayStore(_cfg, _log))
                .Build();

            await new SmtpServer.SmtpServer(options).StartAsync(stoppingToken);
        }
    }
}
