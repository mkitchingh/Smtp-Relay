using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SmtpRelay
{
    /// <summary>Central logging and daily log-file purge.</summary>
    internal static class SmtpLogger
    {
        private static Timer? _purgeTimer;

        /// <summary>Configure Serilog sinks and start daily purge.</summary>
        public static void ConfigureLogging(Config cfg, ILoggerFactory factory)
        {
            Directory.CreateDirectory(Config.SharedLogDir);

            var logPath = Path.Combine(Config.SharedLogDir, "app-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(new RenderedCompactJsonFormatter(),
                              logPath,
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: null)            // we purge manually
                .CreateLogger();

            factory.AddSerilog();

            // Initial purge on startup
            PurgeOldLogs(cfg);

            // Daily purge at 02:00 local
            var now      = DateTime.Now;
            var firstRun = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0)
                           .AddDays(now.Hour >= 2 ? 1 : 0);
            var delayMs  = (int)(firstRun - now).TotalMilliseconds;
            _purgeTimer  = new Timer(_ => PurgeOldLogs(cfg), null, delayMs, TimeSpan.FromDays(1).Milliseconds);
        }

        /// <summary>Delete log files older than <see cref="Config.RetentionDays"/>.</summary>
        private static void PurgeOldLogs(Config cfg)
        {
            if (!cfg.EnableLogging || cfg.RetentionDays <= 0) return;

            try
            {
                var cutoffUtc = DateTime.UtcNow.AddDays(-cfg.RetentionDays);
                foreach (var file in Directory.EnumerateFiles(Config.SharedLogDir, "*.log"))
                {
                    var ts = File.GetLastWriteTimeUtc(file);
                    if (ts < cutoffUtc)
                        File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                // swallow – logging must never crash the service
                Log.Logger.Warning(ex, "Log-purge failed");
            }
        }

        /// <summary>Dispose timer when service stops.</summary>
        public static void Shutdown() => _purgeTimer?.Dispose();
    }
}
