using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Filters.Expressions;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // where we store logs
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service");
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);

            // two rolling-daily files...
            var appLog     = Path.Combine(logDir, "app-.log");
            var smtpLog    = Path.Combine(logDir, "smtp-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()

                // 1) General application log (everything, unchanged)
                .WriteTo.File(
                    appLog,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)

                // 2) SMTP summary events (your RelayStore/Worker logs)
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(
                        Matching.FromSource("SmtpRelay.Worker"))
                    .WriteTo.File(
                        smtpLog,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7))

                // 3) SMTP protocol trace (all SmtpServer.Protocol.* traffic)
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(
                        Matching.FromSource("SmtpServer"))
                    .WriteTo.File(
                        smtpLog,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7))

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
