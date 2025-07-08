using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTools;          // IPAddressRange.Parse

namespace SmtpRelay
{
    /// <summary>Persistent settings for the relay service.</summary>
    public class Config
    {
        [JsonPropertyName("smartHost")]
        public string SmartHost { get; set; } = "";

        [JsonPropertyName("smartHostPort")]
        public int SmartHostPort { get; set; } = 25;

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";

        [JsonPropertyName("allowedIPs")]
        public List<string> AllowedIPs { get; set; } = new();

        /// <summary>True if <paramref name="ip"/> is inside any entry of <see cref="AllowedIPs"/>.</summary>
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
                        $"Invalid IP or CIDR entry \"{entry}\": {ex.Message}", ex);
                }
            }

            return false;
        }

        public static Config Load(string path)
        {
            if (!File.Exists(path))
                return new Config();

            return JsonSerializer.Deserialize<Config>(File.ReadAllText(path))
                   ?? new Config();
        }

        public void Save(string path)
        {
            var json = JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions { WriteIndented = true });

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }
}
