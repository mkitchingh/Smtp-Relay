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
        // Where EVERY process (service or GUI) stores the file
        private static readonly string ConfigDir  =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         "SMTP Relay");                          // ⇒ %ProgramData%\SMTP Relay
        private static readonly string ConfigPath =
            Path.Combine(ConfigDir, "config.json");

        // ───────── SMTP & credentials ─────────
        [JsonPropertyName("smartHost")]      public string SmartHost      { get; set; } = "";
        [JsonPropertyName("smartHostPort")]  public int    SmartHostPort  { get; set; } = 25;
        [JsonPropertyName("username")]       public string Username       { get; set; } = "";
        [JsonPropertyName("password")]       public string Password       { get; set; } = "";
        [JsonPropertyName("useStartTls")]    public bool   UseStartTls    { get; set; } = true;

        // ───────── IP allow-list ─────────
        [JsonPropertyName("allowAllIPs")]    public bool   AllowAllIPs    { get; set; } = false;
        [JsonPropertyName("allowedIPs")]     public List<string> AllowedIPs { get; set; } = new();

        // Split “10.0.0.0/8, 192.168.0.0/16” → tokens, trim, dedupe
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
            if (AllowAllIPs) return true;

            foreach (var entry in AllowedIPs)
            {
                try
                {
                    if (IPAddressRange.Parse(entry).Contains(IPAddress.Parse(ip)))
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

        // ───────── Logging ─────────
        [JsonPropertyName("enableLogging")]  public bool EnableLogging { get; set; } = true;
        [JsonPropertyName("retentionDays")]  public int  RetentionDays { get; set; } = 14;

        // ───────── Load / save ─────────
        public static Config Load(string? path = null)
        {
            path ??= ConfigPath;
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new()
                : new();
        }

        public void Save(string? path = null)
        {
            NormaliseAllowedIPs();                    // split + validate list
            foreach (var entry in AllowedIPs)
                _ = IPAddressRange.Parse(entry);      // throws if bad

            path ??= ConfigPath;
            Directory.CreateDirectory(ConfigDir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }
}
