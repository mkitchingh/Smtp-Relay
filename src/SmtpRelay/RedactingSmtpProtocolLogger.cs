using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MailKit;

namespace SmtpRelay
{
    /// <summary>
    /// Protocol logger that redacts SMTP DATA body content while preserving
    /// the rest of the SMTP transcript (including headers). Also redacts Subject.
    /// </summary>
    internal sealed class RedactingSmtpProtocolLogger : IProtocolLogger, IDisposable
    {
        private sealed class NoOpSecretDetector : IAuthenticationSecretDetector
        {
            public IList<AuthenticationSecret> DetectSecrets(byte[] buffer, int offset, int count)
            {
                // We are not attempting to detect secrets here (AUTH lines are already masked by MailKit
                // in many cases). Returning empty keeps this simple and safe.
                return Array.Empty<AuthenticationSecret>();
            }
        }

        private readonly object _lock = new();
        private readonly StreamWriter _writer;

        private readonly StringBuilder _clientLineBuffer = new();
        private readonly StringBuilder _serverLineBuffer = new();

        private bool _sawDataCommand;
        private bool _inData;
        private bool _pastHeaderBlankLine;
        private bool _redactionLineWritten;

        // MailKit requires this property (get/set) on newer versions.
        public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }

        public RedactingSmtpProtocolLogger(string filePath, bool append = true)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var stream = new FileStream(
                filePath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);

            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            AuthenticationSecretDetector = new NoOpSecretDetector();
        }

        public void LogConnect(Uri uri) => WriteLine($"Connected to {uri}");

        public void LogClient(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;
            var text = Encoding.ASCII.GetString(buffer, offset, count);
            ProcessChunk(text, isClient: true);
        }

        public void LogServer(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;
            var text = Encoding.ASCII.GetString(buffer, offset, count);
            ProcessChunk(text, isClient: false);
        }

        public void LogDisconnect(Uri uri) => WriteLine($"Disconnected from {uri}");

        private void ProcessChunk(string chunk, bool isClient)
        {
            var sb = isClient ? _clientLineBuffer : _serverLineBuffer;
            sb.Append(chunk);

            while (true)
            {
                var s = sb.ToString();
                var idx = s.IndexOf('\n');
                if (idx < 0) break;

                var line = s.Substring(0, idx + 1);
                sb.Clear();
                sb.Append(s.Substring(idx + 1));

                HandleLine(line, isClient);
            }
        }

        private void HandleLine(string rawLine, bool isClient)
        {
            var line = rawLine.TrimEnd('\r', '\n');

            if (isClient)
            {
                // Detect DATA command
                if (!_inData && line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    _sawDataCommand = true;
                    WriteLine($"C: {line}");
                    return;
                }

                if (_inData)
                {
                    // End of DATA
                    if (line == ".")
                    {
                        WriteLine("C: .");
                        ExitDataMode();
                        return;
                    }

                    // Still in headers until blank line
                    if (!_pastHeaderBlankLine)
                    {
                        if (IsSubjectHeader(line))
                            WriteLine("C: Subject: [REDACTED]");
                        else
                            WriteLine($"C: {line}");

                        if (line.Length == 0)
                            _pastHeaderBlankLine = true;

                        return;
                    }

                    // Past headers => redact body lines
                    if (!_redactionLineWritten)
                    {
                        WriteLine("C: [REDACTED BODY]");
                        _redactionLineWritten = true;
                    }

                    // Skip logging actual body lines
                    return;
                }

                // Normal client logging (non-DATA)
                WriteLine($"C: {line}");
                return;
            }

            // Server logging
            WriteLine($"S: {line}");

            // Enter DATA mode when server returns 354 after DATA
            if (_sawDataCommand && !_inData && line.StartsWith("354", StringComparison.Ordinal))
                EnterDataMode();
        }

        private static bool IsSubjectHeader(string line)
        {
            // Very small + safe: only match "Subject:" at line start ignoring case.
            return line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase);
        }

        private void EnterDataMode()
        {
            _inData = true;
            _sawDataCommand = false;
            _pastHeaderBlankLine = false;
            _redactionLineWritten = false;
        }

        private void ExitDataMode()
        {
            _inData = false;
            _sawDataCommand = false;
            _pastHeaderBlankLine = false;
            _redactionLineWritten = false;
        }

        private void WriteLine(string line)
        {
            lock (_lock)
            {
                _writer.WriteLine(line);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer.Dispose();
            }
        }
    }
}