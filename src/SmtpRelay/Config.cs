using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
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
        public static readonly string SharedBaseDir;
        public static readonly string SharedConfigPath;
        public static readonly string SharedLogDir;

        static Config()
        {
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Directory.GetParent(baseDir);

            SharedBaseDir = parent != null ? parent.FullName : AppContext.BaseDirectory;
            SharedConfigPath = Path.Combine(SharedBaseDir, "config.json");
            SharedLogDir = Path.Combine(SharedBaseDir, "logs");
        }

        public string SmartHost { get; set; } = "";
        public int SmartHostPort { get; set; } = 25;

        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        // Legacy
        public bool UseStartTls { get; set; } = false;

        // New explicit mode (optional)
        public OutboundSecurityMode? OutboundSecurity { get; set; } = null;

        public bool AllowAllIPs { get; set; } = true;
        public List<string> AllowedIPs { get; set; } = new List<string>();

        public bool EnableLogging { get; set; } = true;
        public int RetentionDays { get; set; } = 14;

        public OutboundSecurityMode GetEffectiveSecurity()
        {
            if (OutboundSecurity.HasValue)
                return OutboundSecurity.Value;

            return UseStartTls ? OutboundSecurityMode.StartTls : OutboundSecurityMode.None;
        }

        public bool IsIPAllowed(string? remoteIpString)
        {
            if (AllowAllIPs) return true;
            if (string.IsNullOrWhiteSpace(remoteIpString)) return false;

            if (!IPAddress.TryParse(remoteIpString.Trim(), out var parsed))
                return false;

            return IsIPAllowed(parsed);
        }

        public bool IsIPAllowed(IPAddress? remoteIp)
        {
            if (AllowAllIPs) return true;
            if (remoteIp == null) return false;
            if (AllowedIPs.Count == 0) return false;

            foreach (var raw in AllowedIPs)
            {
                var entry = (raw ?? "").Trim();
                if (entry.Length == 0) continue;

                if (IPAddress.TryParse(entry, out var exact))
                {
                    if (exact.Equals(remoteIp)) return true;
                    continue;
                }

                if (TryParseCidr(entry, out var network, out var prefix))
                {
                    if (IsInCidr(remoteIp, network, prefix)) return true;
                    continue;
                }

                if (TryParseDashRange(entry, out var start, out var end))
                {
                    if (IsInRange(remoteIp, start, end)) return true;
                    continue;
                }
            }

            return false;
        }

        private static bool TryParseCidr(string s, out IPAddress network, out int prefixLength)
        {
            network = IPAddress.None;
            prefixLength = 0;

            var parts = s.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0].Trim(), out network))
                return false;

            if (!int.TryParse(parts[1].Trim(), out prefixLength))
                return false;

            int max = network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            return prefixLength >= 0 && prefixLength <= max;
        }

        private static bool TryParseDashRange(string s, out IPAddress start, out IPAddress end)
        {
            start = IPAddress.None;
            end = IPAddress.None;

            var parts = s.Split('-', 2);
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0].Trim(), out start)) return false;
            if (!IPAddress.TryParse(parts[1].Trim(), out end)) return false;

            if (start.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
            if (end.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;

            return ToBigInt(start) <= ToBigInt(end);
        }

        private static bool IsInCidr(IPAddress ip, IPAddress network, int prefixLength)
        {
            if (ip.AddressFamily != network.AddressFamily) return false;

            var ipVal = ToBigInt(ip);
            var netVal = ToBigInt(network);

            int bits = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            int hostBits = bits - prefixLength;

            if (hostBits == bits)
                return true;

            BigInteger mask = (BigInteger.One << bits) - 1;
            mask = mask ^ ((BigInteger.One << hostBits) - 1);

            return (ipVal & mask) == (netVal & mask);
        }

        private static bool IsInRange(IPAddress ip, IPAddress start, IPAddress end)
        {
            if (ip.AddressFamily != start.AddressFamily) return false;
            if (ip.AddressFamily != end.AddressFamily) return false;

            var v = ToBigInt(ip);
            var a = ToBigInt(start);
            var b = ToBigInt(end);

            return v >= a && v <= b;
        }

        private static BigInteger ToBigInt(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            var unsigned = new byte[bytes.Length + 1];

            for (int i = 0; i < bytes.Length; i++)
                unsigned[i] = bytes[bytes.Length - 1 - i];

            unsigned[unsigned.Length - 1] = 0;
            return new BigInteger(unsigned);
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
            opts.Converters.Add(new JsonStringEnumConverter());

            var deserialized = JsonSerializer.Deserialize<Config>(json, opts);
            var cfg = deserialized ?? new Config();

            if (cfg.SmartHost == null) cfg.SmartHost = "";
            if (cfg.Username == null) cfg.Username = "";
            if (cfg.Password == null) cfg.Password = "";
            if (cfg.AllowedIPs == null) cfg.AllowedIPs = new List<string>();
            if (cfg.SmartHostPort <= 0) cfg.SmartHostPort = 25;

            return cfg;
        }

        public void Save()
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            opts.Converters.Add(new JsonStringEnumConverter());

            var json = JsonSerializer.Serialize(this, opts);
            File.WriteAllText(SharedConfigPath, json);
        }
    }
}