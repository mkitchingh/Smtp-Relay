using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SmtpRelay
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            // configure Serilog *before* building the host
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service", "logs");
            Logging.Configure(logDir);

            try
            {
                Log.Information("Starting SMTP Relay service…");

                Host.CreateDefaultBuilder(args)
                    .UseSerilog()  // hook Serilog in
                    .ConfigureServices((hostCtx, services) =>
                    {
                        services.AddSingleton<Config>();
                        services.AddSingleton<MailSender>();
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
