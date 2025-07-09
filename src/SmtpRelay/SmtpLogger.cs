using System.IO;
using Serilog;

namespace SmtpRelay
{
    internal static class SmtpLogger
    {
        public static ILogger Initialise(Config cfg)
        {
            // One canonical folder for BOTH app and SMTP logs
            var logDir = Config.SharedLogDir;            // ← no AppContext.BaseDirectory
            Directory.CreateDirectory(logDir);

            return Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(logDir, "app-.log"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: cfg.RetentionDays)
                .CreateLogger();
        }
    }
}
