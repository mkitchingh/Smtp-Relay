using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Storage;

namespace SmtpRelay
{
    public class Worker : BackgroundService
    {
        private readonly Config _cfg;
        private readonly ILogger _log;
        private SmtpServer.SmtpServer _server;

        public Worker()
        {
            _cfg = Config.Load();
            _log = Log.ForContext<Worker>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var builder = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                // listen on port (no unencrypted fallback)
                .Endpoint(e => e
                    .Port(_cfg.ListenPort, false)
                );

            var options = builder.Build();

            // SmtpServer uses built-in IoC; register only the relay store
            var serviceProvider = new ServiceProviderBuilder()
                .AddMessageStore(sp => new MessageRelayStore(_cfg, _log))
                .BuildServiceProvider();

            _server = new SmtpServer.SmtpServer(options, serviceProvider);

            try
            {
                _log.Information("SMTP Server starting on port {Port}", _cfg.ListenPort);
                await _server.StartAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, "SMTP Server failed to start");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_server != null)
            {
                _log.Information("Stopping SMTP Server");
                await _server.StopAsync(cancellationToken).ConfigureAwait(false);
            }

            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
