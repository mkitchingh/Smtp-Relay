using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NetTools;

namespace SmtpRelay
{
    public class Config
    {
        public string SmartHost     { get; set; } = "";
        public int    SmartHostPort { get; set; } = 25;
        public string Username      { get; set; } = "";
        public string Password      { get; set; } = "";
        public bool   UseStartTls   { get; set; } = false;

        public bool AllowAllIPs       { get; set; } = true;
        public List<string> AllowedIPs { get; set; } = new();

        public bool EnableLogging { get; set; } = false;
        public int  RetentionDays { get; set; } = 30;

        // Shared path under Program Files\SMTP Relay\config.json
        private static string FilePath
        {
            get
            {
                var baseDir = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);
                var dir     = Path.Combine(baseDir, "SMTP Relay");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.json");
            }
        }

        public static Config Load()
        {
            var path = FilePath;
            if (!File.Exists(path)) return new Config();
            return JsonSerializer
                .Deserialize<Config>(File.ReadAllText(path))
                ?? new Config();
        }

        /// <summary>
        /// Validates and saves the config to the shared path.
        /// Throws FormatException on invalid entries, IOException on write failure.
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(SmartHost))
                throw new FormatException("SMTP Host must not be empty.");

            if (!AllowAllIPs)
            {
                // Flatten any comma-separated entries into individual items
                var normalized = new List<string>();
                foreach (var raw in AllowedIPs)
                {
                    var parts = raw
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => p.Length > 0);
                    normalized.AddRange(parts);
                }

                // Validate each piece and then replace the property with the clean list
                for (int i = 0; i < normalized.Count; i++)
                {
                    var entry = normalized[i];
                    try
                    {
                        _ = IPAddressRange.Parse(entry);
                    }
                    catch (Exception ex)
                    {
                        throw new FormatException(
                            $"Invalid IP or CIDR entry \"{entry}\": {ex.Message}");
                    }
                }

                AllowedIPs = normalized;
            }

            var json = JsonSerializer.Serialize(
                this, new JsonSerializerOptions { WriteIndented = true });

            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"Failed to write config file at {FilePath}:\n{ex.Message}");
            }
        }
    }
}
