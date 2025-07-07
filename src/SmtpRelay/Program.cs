using System;
using System.IO;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // prepare log directories
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var servicePath = Path.Combine(basePath, "SMTP Relay", "service");
            var logPath     = Path.Combine(servicePath, "logs");
            Directory.CreateDirectory(logPath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()

                // your existing application log sink
                .WriteTo.File(
                    path: Path.Combine(logPath, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )

                // new: all SMTP‐server + MailKit events into one smtp-*.log
                .WriteTo.Logger(lc => lc
                    // filter on events coming from our SMTP server implementation
                    .Filter.ByIncludingOnly(evt =>
                        evt.Properties.ContainsKey("SourceContext") &&
                        (
                            evt.Properties["SourceContext"].ToString().Contains("SmtpRelay.Worker") ||
                            evt.Properties["SourceContext"].ToString().Contains("MailKit") ||
                            evt.Properties["SourceContext"].ToString().Contains("SmtpServer")
                        )
                    )
                    .WriteTo.File(
                        path: Path.Combine(logPath, "smtp-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 31,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )
                )
                .CreateLogger();

            try
            {
                Log.Information("Starting up SMTP Relay");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog() // wire Serilog into the Generic Host
                .ConfigureServices((hostCtx, services) =>
                {
                    services.AddHostedService<Worker>();
                });
    }
}
