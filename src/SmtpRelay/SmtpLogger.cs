using System;
using System.IO;
using Serilog;

namespace SmtpRelay
{
    internal static class SmtpLogger
    {
        public static ILogger Initialise(Config cfg)
        {
            if (!cfg.EnableLogging)
                return Log.Logger = new LoggerConfiguration().CreateLogger();

            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SMTP Relay", "logs");

            Directory.CreateDirectory(logDir);

            // ───── Application log (one file per day) ─────
            var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logDir, "app-.log"),            // Serilog adds YYYYMMDD
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: cfg.RetentionDays)
                .CreateLogger();

            return Log.Logger = logger;
        }
    }
}
