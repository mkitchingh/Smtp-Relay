using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // Load config once and initialise Serilog (shared log folder)
            var cfg = Config.Load();
            SmtpLogger.Initialise(cfg);

            try
            {
                Log.Information("Starting SMTP Relay Service");

                Host.CreateDefaultBuilder(args)
                    .UseWindowsService()           // runs as NT service or console
                    .UseSerilog()                  // replaces default logging
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(cfg); // shared instance
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
