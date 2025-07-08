using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTools;                  // from the IPAddressRange package

namespace SmtpRelay
{
    /// <summary>Settings that the relay service persists on disk.</summary>
    public sealed class Config
    {
        // ────────────────────────────────────────────────────────────
        //  SMTP / relay settings
        // ────────────────────────────────────────────────────────────
        [JsonPropertyName("smartHost")]
        public string SmartHost { get; set; } = "";

        [JsonPropertyName("smartHostPort")]
        public int SmartHostPort { get; set; } = 25;

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";

        [JsonPropertyName("useStartTls")]
        public bool UseStartTls { get; set; } = true;

        // ────────────────────────────────────────────────────────────
        //  IP allow-list
        // ────────────────────────────────────────────────────────────
        [JsonPropertyName("allowAllIPs")]
        public bool AllowAllIPs { get; set; } = false;

        [JsonPropertyName("allowedIPs")]
        public List<string> AllowedIPs { get; set; } = new();

        /// <summary>
        /// Split on commas / semicolons / whitespace, trim, dedup.
        /// </summary>
        private void NormaliseAllowedIPs()
        {
            char[] delims = { ',', ';', ' ', '\t', '\n', '\r' };

            AllowedIPs = AllowedIPs
                .SelectMany(s => s.Split(delims, StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool IsIPAllowed(string ip)
        {
            if (AllowAllIPs)
                return true;

            foreach (var entry in AllowedIPs)
            {
                try
                {
                    if (IPAddressRange.Parse(entry)
                                      .Contains(IPAddress.Parse(ip)))
                        return true;
                }
                catch (Exception ex)
                {
                    throw new FormatException(
                        $"Invalid IP/CIDR entry \"{entry}\": {ex.Message}", ex);
                }
            }
            return false;
        }

        // ────────────────────────────────────────────────────────────
        //  Logging options
        // ────────────────────────────────────────────────────────────
        [JsonPropertyName("enableLogging")]
        public bool EnableLogging { get; set; } = true;

        [JsonPropertyName("retentionDays")]
        public int RetentionDays { get; set; } = 14;

        // ────────────────────────────────────────────────────────────
        //  Load / save helpers
        // ────────────────────────────────────────────────────────────
        public static Config Load(string? path = null)
        {
            path ??= Path.Combine(AppContext.BaseDirectory, "config.json");
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new()
                : new();
        }

        public void Save(string? path = null)
        {
            NormaliseAllowedIPs();                 // ensure list is tokenised & deduped

            // Validate every entry up front
            foreach (var entry in AllowedIPs)
                _ = IPAddressRange.Parse(entry);

            path ??= Path.Combine(AppContext.BaseDirectory, "config.json");

            var json = JsonSerializer.Serialize(
                this, new JsonSerializerOptions { WriteIndented = true });

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
