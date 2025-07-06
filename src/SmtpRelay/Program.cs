using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // Shared log folder under Program Files\SMTP Relay\service\logs
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            // Paths for the two rolling logs
            var appLogPath  = Path.Combine(logDir, "app-.log");
            var smtpLogPath = Path.Combine(logDir, "smtp-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                // 1) Your existing app log
                .WriteTo.File(
                    appLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                // 2) A second log that captures every event
                .WriteTo.File(
                    smtpLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .CreateLogger();

            try
            {
                Log.Information("Starting SMTP Relay Service");
                Host.CreateDefaultBuilder(args)
                    .UseWindowsService()
                    .UseSerilog()
                    .ConfigureServices((_, services) =>
                        services.AddHostedService<Worker>())
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
