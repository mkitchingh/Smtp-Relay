using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtpServer;
using SmtpServer.Logging;
using SmtpServer.Storage;
using SmtpServer.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace SmtpRelay
{
    public class Worker : BackgroundService
    {
        private readonly Config        _cfg;
        private readonly Serilog.ILogger _log;

        public Worker()
        {
            _cfg = Config.Load();
            _log = Log.Logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Endpoint(builder => builder.Port(_cfg.SmartHostPort).Build())
                .Build();

            var serviceProvider = new ServiceProviderBuilder()
                .AddMessageStore(new MessageRelayStore(_cfg, _log))
                .AddProtocolLogger(new ConsoleProtocolLogger())  // logs protocol to console
                .BuildServiceProvider();

            var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);

            await smtpServer.StartAsync(stoppingToken);
        }
    }
}
