using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Filters;

namespace SmtpRelay
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            // ensure service folder + log folder
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()

                // 1) General application log:
                .WriteTo.File(
                    Path.Combine(logDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)   // or hook into your RetentionDays

                // 2) Single SMTP log (merges both relay-events & protocol trace)
                .WriteTo.Logger(lc => lc
                    // include any events from your Worker (SmtpRelay namespace)…
                    .Filter.ByIncludingOnly(Matching.FromSource("SmtpRelay.Worker"))
                    // …and all the low-level protocol logs under "SmtpServer"
                    .Filter.ByIncludingOnly(Matching.FromSource("SmtpServer"))
                    .WriteTo.File(
                        Path.Combine(logDir, "smtp-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30))

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
