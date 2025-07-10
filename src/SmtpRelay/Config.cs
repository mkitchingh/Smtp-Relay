using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTools;

namespace SmtpRelay
{
    public sealed class Config
    {
        // ───────── shared root:  …\SMTP Relay\  ─────────
        private static readonly string RootDir =
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        private static readonly string ConfigPath = Path.Combine(RootDir, "config.json");

        // SMTP / credentials
        [JsonPropertyName("smartHost")]     public string SmartHost { get; set; } = "";
        [JsonPropertyName("smartHostPort")] public int    SmartHostPort { get; set; } = 25;
        [JsonPropertyName("username")]      public string Username { get; set; } = "";
        [JsonPropertyName("password")]      public string Password { get; set; } = "";
        [JsonPropertyName("useStartTls")]   public bool   UseStartTls { get; set; } = false;

        // IP allow-list
        [JsonPropertyName("allowAllIPs")] public bool          AllowAllIPs { get; set; } = true;
        [JsonPropertyName("allowedIPs")]  public List<string>  AllowedIPs  { get; set; } = new();

        // Logging
        [JsonPropertyName("enableLogging")] public bool EnableLogging { get; set; } = true;
        [JsonPropertyName("retentionDays")] public int  RetentionDays { get; set; } = 14;

        // ───────── helpers ─────────
        public bool IsIPAllowed(string ip)
        {
            if (AllowAllIPs) return true;
            return AllowedIPs.Any(r => IPAddressRange.Parse(r).Contains(IPAddress.Parse(ip)));
        }

        public static Config Load()
        {
            return File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new()
                : new();
        }

        public void Save()
        {
            NormaliseAllowedIPs();
            foreach (var r in AllowedIPs) _ = IPAddressRange.Parse(r); // validate
            Directory.CreateDirectory(RootDir);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void NormaliseAllowedIPs()
        {
            char[] delims = { ',', ';', ' ', '\t', '\n', '\r' };
            AllowedIPs = AllowedIPs
                .SelectMany(s => s.Split(delims, StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Expose shared log dir to other classes
        public static string SharedLogDir => Path.Combine(RootDir, "logs");
    }
}
