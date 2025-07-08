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

            // Logs beside the service EXE → C:\Program Files\SMTP Relay\service\logs
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logDir, "app-.log"),       // Serilog adds YYYYMMDD
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: cfg.RetentionDays)
                .CreateLogger();

            return Log.Logger = logger;
        }
    }
}
