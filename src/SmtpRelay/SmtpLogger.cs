using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace SmtpRelay
{
    internal static class SmtpLogger
    {
        private static readonly object InitLock = new();
        private static bool   _initialised;
        private static Timer? _purgeTimer;

        /* ------------------------------------------------------------------
           PUBLIC API (signature unchanged)
           ------------------------------------------------------------------ */

        public static Serilog.ILogger Initialise(Config cfg)
        {
            var factory = LoggerFactory.Create(b => b.AddSerilog());
            return Initialise(cfg, factory);
        }

        public static Serilog.ILogger Initialise(Config cfg, ILoggerFactory factory)
        {
            lock (InitLock)
            {
                if (_initialised) return Log.Logger;   // <─ prevents _001 files

                Configure(cfg, factory);
                _initialised = true;
                return Log.Logger;
            }
        }

        public static void Shutdown() => _purgeTimer?.Dispose();

        /* ------------------------------------------------------------------
           PRIVATE IMPLEMENTATION
           ------------------------------------------------------------------ */

        private static void Configure(Config cfg, ILoggerFactory factory)
        {
            Directory.CreateDirectory(Config.SharedLogDir);

            const string template =
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: Path.Combine(Config.SharedLogDir, "app-.log"),
                    outputTemplate: template,               // ← plain text
                    rollingInterval: RollingInterval.Day,
                    shared: true,                           // safe across threads
                    retainedFileCountLimit: null)
                .CreateLogger();

            factory.AddSerilog();

            PurgeOldLogs(cfg);        // startup purge
            ScheduleDailyPurge(cfg);  // daily at 02:00
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
