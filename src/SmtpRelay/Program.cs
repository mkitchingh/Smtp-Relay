using System;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SmtpRelay
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: "app-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .WriteTo.File(
                    path: "smtp-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .CreateLogger();

            try
            {
                Log.Information("SMTP Relay starting up");
                Host.CreateDefaultBuilder(args)
                    .UseSerilog()
                    .ConfigureServices((_, services) =>
                    {
                        services.AddSingleton<Config>();
                        services.AddHostedService<Worker>();
                    })
                    .Build()
                    .Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Unhandled exception");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
