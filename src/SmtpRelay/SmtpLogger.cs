using System;
using System.IO;
using Serilog;

namespace SmtpRelay
{
    public static class SmtpLogger
    {
        public static readonly ILogger Logger;

        static SmtpLogger()
        {
            // Mirror the retention setting
            var cfg = Config.Load();
            var retention = cfg.RetentionDays;

            // Ensure logs folder exists
            var logDir = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service", "logs");
            Directory.CreateDirectory(logDir);

            // SMTP log path
            var smtpLogPath = Path.Combine(logDir, "smtp-.log");

            // Configure Serilog for SMTP traffic only
            Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    smtpLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retention)
                .CreateLogger();
        }
    }
}
