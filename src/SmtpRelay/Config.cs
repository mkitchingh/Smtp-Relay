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

        /// <summary>
        /// Returns true if <paramref name="ip"/> lies inside any entry of
        /// <see cref="AllowedIPs"/> (single IP or CIDR).
        /// </summary>
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

        /// <summary>Load settings from <paramref name="path"/> (or defaults if missing).</summary>
        public static Config Load(string path)
        {
            if (!File.Exists(path))
                return new Config();
