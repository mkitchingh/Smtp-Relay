using System.IO;
using Serilog;

namespace SmtpRelay
{
    internal static class SmtpLogger
    {
        public static ILogger Initialise(Config cfg)
        {
            var logDir = Config.SharedLogDir;     // one canonical folder
            Directory.CreateDirectory(logDir);

            Log.CloseAndFlush();  // reset if already configured

            return Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(logDir, "app-.log"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: cfg.RetentionDays)
                .CreateLogger();
        }
    }
}
