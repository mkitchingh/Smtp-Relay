using System;
using System.IO;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // ensure log directory exists
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service", "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()

                // general application log
                .WriteTo.File(
                    Path.Combine(logDir, "app-.log"),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day
                )

                // ← REMOVED the sub-logger that wrote plain smtp-*.log

                .CreateLogger();

            try
            {
                Log.Information("Starting SMTP Relay Service");

                var cfg = Config.Load();
                Log.Information(
                    "Relay mode: {Mode}",
                    cfg.AllowAllIPs
                        ? "Allow All"
                        : $"Allow {cfg.AllowedIPs.Count} range(s)");

                Host.CreateDefaultBuilder(args)
                    .UseSerilog()
                    .ConfigureServices((_, services) =>
                    {
                        services.AddSingleton(cfg);
                        services.AddHostedService<Worker>();
                    })
                    .Build()
                    .Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Service terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
