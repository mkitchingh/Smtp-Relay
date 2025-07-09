using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // Load config once, initialise Serilog to the shared log folder
            var cfg = Config.Load();
            SmtpLogger.Initialise(cfg);

            try
            {
                Log.Information("Starting SMTP Relay Service");

                Host.CreateDefaultBuilder(args)
                    .UseWindowsService()                    // runs as NT service or console fallback
                    .ConfigureLogging(lb => lb.ClearProviders()) // Serilog only
                    .UseSerilog()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(cfg);
                        services.AddHostedService<Worker>();
                    })
                    .Build()
                    .Run();
            }
            finally
            {
                Log.Information("SMTP Relay Service shutting down");
                Log.CloseAndFlush();
            }
        }
    }
}
