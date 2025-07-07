using System;
using System.IO;
using Serilog;

namespace SmtpRelay
{
    public static class Logging
    {
        public static void Configure(string logDirectory)
        {
            // ensure the directory exists
            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                // write everything into one rolling daily file:
                .WriteTo.File(
                    path: Path.Combine(logDirectory, $"smtp-{DateTime.Now:yyyyMMdd}.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();
        }
    }
}
