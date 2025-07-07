using System;
using System.IO;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

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

                // smtp‐conversation log: filter by SourceContext == "SmtpServer"
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(new Predicate<LogEvent>(evt =>
                    {
                        if (!evt.Properties.TryGetValue("SourceContext", out var sc)) return false;
                        // sc.ToString() is "\"SmtpServer.Protocol.SmtpServer\""... just check prefix:
                        return sc.ToString().StartsWith("\"SmtpServer");
                    }))
                    .WriteTo.File(
                        Path.Combine(logDir, "smtp-.log"),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [INF] {Message:lj}{NewLine}",
                        rollingInterval: RollingInterval.Day
                    )
                )

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
