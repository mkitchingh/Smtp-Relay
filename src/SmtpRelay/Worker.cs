using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTools;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpRelay
{
    public class Worker : BackgroundService
    {
        readonly ILogger<Worker>   _log;
        readonly Config            _cfg;
        readonly IPAddressRange[]  _ranges;

        public Worker(ILogger<Worker> log)
        {
            _log   = log;
            _cfg   = Config.Load();
            _ranges = _cfg.AllowAllIPs
                ? Array.Empty<IPAddressRange>()
                : _cfg.AllowedIPs.Select(IPAddressRange.Parse).ToArray();

            _log.LogInformation(_cfg.AllowAllIPs
                ? "Relay mode: Allow ALL IPs"
                : $"Relay mode: Allow {_ranges.Length} range(s)");
        }

        protected override Task ExecuteAsync(CancellationToken token)
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("SMTP Relay")
                .Port(25)
                .Build();

            var provider = new ServiceProvider();
            provider.Add(new RelayStore(_cfg, _ranges, _log));

            _log.LogInformation("SMTP Relay listening on port 25");
            return new SmtpServer.SmtpServer(options, provider)
                .StartAsync(token);
        }

        /// <summary>
        /// Enforces the IP allow-list, logs via both “app” and “smtp” loggers,
        /// and relays mail when permitted.
        /// </summary>
        private sealed class RelayStore : MessageStore
        {
            readonly Config            _cfg;
            readonly IPAddressRange[]  _ranges;
            readonly ILogger           _log;

            public RelayStore(
                Config cfg,
                IPAddressRange[] ranges,
                ILogger log)
            {
                _cfg    = cfg;
                _ranges = ranges;
                _log    = log;
            }

            public override async Task<SmtpResponse> SaveAsync(
                ISessionContext     context,
                IMessageTransaction transaction,
                ReadOnlySequence<byte> buffer,
                CancellationToken   cancellationToken)
            {
                // 1) Extract the *client* IP, skipping server-listeners (0.0.0.0/::)
                IPAddress? ip = context.Properties.Values
                    .OfType<IPEndPoint>()
                    .Where(ep =>
                        !IPAddress.Any.Equals(ep.Address) &&
                        !IPAddress.IPv6Any.Equals(ep.Address))
                    .Select(ep => ep.Address)
                    .FirstOrDefault();

                // 2) Log the attempt
                _log.LogInformation("Incoming relay request from {IP}", ip);
                SmtpLogger.Logger.Information("Incoming relay request from {IP}", ip);

                // 3) Enforce allow-list
                bool allowed = _cfg.AllowAllIPs
                            || _ranges.Length == 0
                            || (ip != null && _ranges.Any(r => r.Contains(ip)));

                if (!allowed)
                {
                    _log.LogWarning("DENIED {IP} — not in allow-list", ip);
                    SmtpLogger.Logger.Warning("DENIED {IP} — not in allow-list", ip);

                    return new SmtpResponse(
                        SmtpReplyCode.MailboxUnavailable,
                        "550 Relaying Denied");
                }

                // 4) Forward mail
                try
                {
                    await MailSender.SendAsync(
                        _cfg, buffer, cancellationToken);

                    _log.LogInformation("Relayed mail from {IP}", ip);
                    SmtpLogger.Logger.Information("Relayed mail from {IP}", ip);

                    return SmtpResponse.Ok;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Relay failure from {IP}", ip);
                    SmtpLogger.Logger.Error(ex, "Relay failure from {IP}", ip);

                    return SmtpResponse.TransactionFailed;
                }
            }
        }
    }
}
