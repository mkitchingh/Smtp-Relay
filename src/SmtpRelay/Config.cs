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
    /// <summary>Persistent settings for the relay service.</summary>
    public sealed class Config
    {
        // Location: same folder as the service EXE
        private static readonly string ConfigPath =
            Path.Combine(AppContext.BaseDirectory, "config.json");

        // ───── SMTP / smart-host settings ─────
        [JsonPropertyName("smartHost")]     public string SmartHost     { get; set; } = "";
        [JsonPropertyName("smartHostPort")] public int    SmartHostPort { get; set; } = 25;
        [JsonPropertyName("username")]      public string Username      { get; set; } = "";
        [JsonPropertyName("password")]      public string Password      { get; set; } = "";
        [JsonPropertyName("useStartTls")]   public bool   UseStartTls   { get; set; } = true;

        // ───── IP allow-list ─────
        [JsonPropertyName("allowAllIPs")] public bool          AllowAllIPs { get; set; } = false;
        [JsonPropertyName("allowedIPs")]  public List<string>  AllowedIPs  { get; set; } = new();

        // ───── Logging options ─────
        [JsonPropertyName("enableLogging")] public bool EnableLogging { get; set; } = true;
        [JsonPropertyName("retentionDays")] public int  RetentionDays { get; set; } = 14;

        // ─────────────────────────────────────────────────────────────

        /// <summary>True if <paramref name="ip"/> is in any range.</summary>
        public bool IsIPAllowed(string ip)
        {
            if (AllowAllIPs) return true;
            foreach (var entry in AllowedIPs)
                if (IPAddressRange.Parse(entry).Contains(IPAddress.Parse(ip)))
                    return true;
            return false;
        }

        /// <summary>Load settings (creates defaults if file missing).</summary>
        public static Config Load()
        {
            return File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new()
                : new();
        }

        /// <summary>Save settings back to <c>config.json</c>.</summary>
        public void Save()
        {
            NormaliseAllowedIPs();
            foreach (var entry in AllowedIPs) _ = IPAddressRange.Parse(entry); // validate

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        // ───── helper: split comma-/newline-separated input ─────
        private void NormaliseAllowedIPs()
        {
            char[] delims = { ',', ';', ' ', '\t', '\n', '\r' };
            AllowedIPs = AllowedIPs
                .SelectMany(s => s.Split(delims, StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
