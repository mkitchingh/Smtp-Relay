using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>True if <paramref name="ip"/> is inside any CIDR/IP in <see cref="AllowedIPs"/>.</summary>
        public bool IsIPAllowed(string ip)
        {
            foreach (var entry in AllowedIPs)
            {
                try
                {
                    var range = IPAddressRange.Parse(entry);
                    if (range.Contains(IPAddress.Parse(ip)))
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

        /// <summary>Load config – defaults to *config.json* in the service folder.</summary>
        public static Config Load(string? path = null)
        {
            path ??= Path.Combine(AppContext.BaseDirectory, "config.json");
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new()
                : new();
        }

        public void Save(string? path = null)
        {
            path ??= Path.Combine(AppContext.BaseDirectory, "config.json");
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
