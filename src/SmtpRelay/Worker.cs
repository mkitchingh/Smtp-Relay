using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using NetTools;          // ← added to bring IPAddressRange into scope

namespace SmtpRelay
{
    public class Worker : BackgroundService
    {
        readonly Config _cfg;
        readonly MailSender _sender;
        readonly ILogger _log;

        public Worker(Config cfg, MailSender sender, ILogger log)
        {
            _cfg = cfg;
            _sender = sender;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var builder = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(_cfg.ListenPort)  // ListenPort must be on Config
                .MessageStore(new MessageRelayStore(_cfg, _log));

            if (!_cfg.AllowAllIPs)
            {
                // parse each CIDR/text entry into an IPAddressRange
                var ranges = _cfg.AllowedIPs
                    .Select(IPAddressRange.Parse)
                    .ToArray();

                builder = builder.AllowedClientAddresses(ranges);
            }

            // wire up Serilog for protocol-level events
            builder = builder
                .ProtocolLogger(new SerilogProtocolLogger(_log));

            var serviceProvider = new ServiceCollection()
                .AddSingleton(_cfg)
                .AddSingleton(_sender)
                .AddSingleton(_log)
                .BuildServiceProvider();

            var server = new SmtpServer.SmtpServer(builder.Build(), serviceProvider);

            await server.StartAsync(stoppingToken);
        }
    }
}
