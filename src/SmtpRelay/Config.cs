using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmtpRelay
{
    public enum OutboundSecurityMode
    {
        None = 0,
        StartTls = 1,
        Smtps = 2
    }

    public class Config
    {
        // Shared install locations
        public static readonly string SharedBaseDir = AppContext.BaseDirectory;
        public static readonly string SharedConfigPath = Path.Combine(SharedBaseDir, "config.json");
        public static readonly string SharedLogDir = Path.Combine(SharedBaseDir, "logs");

        public string SmartHost { get; set; } = "";
        public int SmartHostPort { get; set; } = 25;

        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        // Legacy flag (existing installs)
        public bool UseStartTls { get; set; } = false;

        // New: explicit outbound security (optional for backward compatibility)
        public OutboundSecurityMode? OutboundSecurity { get; set; } = null;

        public bool AllowAllIPs { get; set; } = true;
        public List<string> AllowedIPs { get; set; } = new();

        public bool EnableLogging { get; set; } = true;
        public int RetentionDays { get; set; } = 14;

        public OutboundSecurityMode GetEffectiveSecurity()
        {
            // Prefer explicit new mode if present; otherwise fall back to legacy UseStartTls
            if (OutboundSecurity.HasValue)
                return OutboundSecurity.Value;

            return UseStartTls ? OutboundSecurityMode.StartTls : OutboundSecurityMode.None;
        }

        public static Config Load()
        {
            if (!File.Exists(SharedConfigPath))
                return new Config();

            var json = File.ReadAllText(SharedConfigPath);

            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            // Allow enum values to be written as strings OR numbers safely
            opts.Converters.Add(new JsonStringEnumConverter());

            var cfg = JsonSerializer.Deserialize<Config>(json, opts) ?? new Config();

            // Defensive defaults
            if (cfg.SmartHostPort <= 0) cfg.SmartHostPort = 25;
            cfg.AllowedIPs ??= new List<string>();

            return cfg;
        }

        public void Save()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            opts.Converters.Add(new JsonStringEnumConverter());

            var json = JsonSerializer.Serialize(this, opts);
            File.WriteAllText(SharedConfigPath, json);
        }
    }
}