using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTools;                  //  from the IPAddressRange package

namespace SmtpRelay
{
    /// <summary>Settings that the relay service persists on disk.</summary>
    public sealed class Config
    {
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

        [JsonPropertyName("allowedIPs")]
        public List<string> AllowedIPs { get; set; } = new();

        /// <summary>
        /// Normalises <see cref="AllowedIPs"/>:
        ///  • splits on commas, semicolons, whitespace, or new-lines  
        ///  • trims each token  
        ///  • removes blanks & duplicates
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

        /// <summary>True if <paramref name="ip"/> lies in any CIDR/IP entry.</summary>
        public bool IsIPAllowed(string ip)
        {
            // No normalisation here—Save() guarantees the list is clean.
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

        /// <summary>Load config – defaults to *config.json* beside the EXE.</summary>
        public static Config Load(string? path = null)
        {
            path ??= Path.Combine(AppContext.BaseDirectory, "config.json");
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new()
                : new();
        }

        /// <summary>
        /// Saves settings to disk after normalising and validating <see cref="AllowedIPs"/>.
        /// Throws <see cref="FormatException"/> if any entry is malformed.
        /// </summary>
        public void Save(string? path = null)
        {
            NormaliseAllowedIPs();                 // 💡 new line

            // Validate every entry before we hit the disk.
            foreach (var entry in AllowedIPs)
            {
                _ = IPAddressRange.Parse(entry);   // will throw if bad
            }

            path ??= Path.Combine(AppContext.BaseDirectory, "config.json");

            var json = JsonSerializer.Serialize(
                this, new JsonSerializerOptions { WriteIndented = true });

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
