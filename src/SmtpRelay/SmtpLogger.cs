using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SmtpRelay
{
    internal static class SmtpLogger
    {
        private static Timer? _purgeTimer;

        /* ------------------------------------------------------------------
           PUBLIC API  (now returns Serilog.ILogger like original build)
           ------------------------------------------------------------------ */

        /// <summary>Initialise logging and daily purge (factory created inside).</summary>
        public static Serilog.ILogger Initialise(Config cfg)
        {
            var factory = LoggerFactory.Create(b => { b.AddSerilog(); });
            return Configure(cfg, factory);
        }

        /// <summary>Initialise logging when an <see cref="ILoggerFactory"/> is already available.</summary>
        public static Serilog.ILogger Initialise(Config cfg, ILoggerFactory factory) =>
            Configure(cfg, factory);

        /// <summary>Dispose purge timer (call from Worker.StopAsync).</summary>
        public static void Shutdown() => _purgeTimer?.Dispose();

        /* ------------------------------------------------------------------ */
        /*  PRIVATE IMPLEMENTATION                                            */
        /* ------------------------------------------------------------------ */

        private static Serilog.ILogger Configure(Config cfg, ILoggerFactory factory)
        {
            Directory.CreateDirectory(Config.SharedLogDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(new RenderedCompactJsonFormatter(),
                              Path.Combine(Config.SharedLogDir, "app-.log"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: null)
                .CreateLogger();

            factory.AddSerilog();

            PurgeOldLogs(cfg);       // first purge on startup
            ScheduleDailyPurge(cfg); // daily purge at 02:00 local

            return Log.Logger;       // <-- return for caller chaining
        }

        private static void ScheduleDailyPurge(Config cfg)
        {
            var now      = DateTime.Now;
            var firstRun = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0);
            if (now >= firstRun) firstRun = firstRun.AddDays(1);
            var delay = firstRun - now;

            _purgeTimer = new Timer(_ => PurgeOldLogs(cfg),
                                     null,
                                     delay,
                                     TimeSpan.FromDays(1));
        }

        private static void PurgeOldLogs(Config cfg)
        {
            if (!cfg.EnableLogging || cfg.RetentionDays <= 0) return;

            var cutoffUtc = DateTime.UtcNow.AddDays(-cfg.RetentionDays);

            try
            {
                foreach (var file in Directory.EnumerateFiles(Config.SharedLogDir, "*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
                        File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Log-purge failed");
            }
        }
    }
}
