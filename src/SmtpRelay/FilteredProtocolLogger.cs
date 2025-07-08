using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using MailKit;

namespace SmtpRelay
{
    /// <summary>
    /// Captures only SMTP commands & replies (skipping DATA bodies),
    /// and writes them as timestamped lines to a daily log.
    /// </summary>
    public class FilteredProtocolLogger : IProtocolLogger, IDisposable
    {
        private readonly string _filePath;
        private readonly object _sync = new object();
        private bool _inData;

        public FilteredProtocolLogger(string filePath)
        {
            _filePath = filePath;
        }

        // Required by interface: no-op detector
        public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; } = new NoOpSecretDetector();

        public void LogConnect(Uri uri)
        {
            WriteLine($"CONNECT → {uri}");
        }

        public void LogClient(byte[] buffer, int offset, int count)
        {
            var line = Encoding.UTF8
                .GetString(buffer, offset, count)
                .TrimEnd('\r', '\n');
            ProcessLine("CLIENT →", line);
        }

        public void LogServer(byte[] buffer, int offset, int count)
        {
            var line = Encoding.UTF8
                .GetString(buffer, offset, count)
                .TrimEnd('\r', '\n');
            ProcessLine("SERVER →", line);
        }

        private void ProcessLine(string prefix, string line)
        {
            // Skip DATA body content until terminator '.'
            if (_inData)
            {
                if (line == ".") _inData = false;
                return;
            }
            if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                _inData = true;
            }
            WriteLine($"{prefix} {line}");
        }

        private void WriteLine(string text)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lock (_sync)
            {
                File.AppendAllText(
                    _filePath,
                    $"{ts} {text}{Environment.NewLine}");
            }
        }

        public void Dispose() { /* nothing to clean up */ }

        // A no-op secret detector so authentication secrets aren't redacted
        private class NoOpSecretDetector : IAuthenticationSecretDetector
        {
            public IList<AuthenticationSecret> DetectSecrets(byte[] buffer, int offset, int count)
            {
                return Array.Empty<AuthenticationSecret>();
            }
        }
    }
}
