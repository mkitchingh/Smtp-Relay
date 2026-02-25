using System;
using System.IO;
using System.Text;
using MailKit;

namespace SmtpRelay
{
    /// <summary>
    /// Protocol logger that redacts SMTP DATA body content while preserving
    /// the rest of the SMTP transcript (including headers).
    /// </summary>
    internal sealed class RedactingSmtpProtocolLogger : IProtocolLogger, IDisposable
    {
        private readonly object _lock = new();
        private readonly StreamWriter _writer;

        private readonly StringBuilder _clientLineBuffer = new();
        private readonly StringBuilder _serverLineBuffer = new();

        private bool _sawDataCommand;
        private bool _inData;
        private bool _pastHeaderBlankLine;
        private bool _redactionLineWritten;

        public RedactingSmtpProtocolLogger(string filePath, bool append = true)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var stream = new FileStream(
                filePath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);

            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        }

        public void LogConnect(Uri uri)
        {
            WriteLine($"Connected to {uri}");
        }

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

        public void LogDisconnect(Uri uri)
        {
            WriteLine($"Disconnected from {uri}");
        }

        private void ProcessChunk(string chunk, bool isClient)
        {
            // Normalize line parsing across chunks.
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
            // Trim only CRLF for analysis; keep the original direction prefix formatting.
            var line = rawLine.TrimEnd('\r', '\n');

            if (isClient)
            {
                // Detect start of DATA command (client side)
                if (!_inData && line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    _sawDataCommand = true;
                    WriteLine($"C: {line}");
                    return;
                }

                // During DATA, redact body after the blank line that separates headers and body.
                if (_inData)
                {
                    // Dot-terminator ends DATA
                    if (line == ".")
                    {
                        WriteLine("C: .");
                        ExitDataMode();
                        return;
                    }

                    // Still in headers until the blank line
                    if (!_pastHeaderBlankLine)
                    {
                        WriteLine($"C: {line}");
                        if (line.Length == 0)
                        {
                            _pastHeaderBlankLine = true;
                        }
                        return;
                    }

                    // Past headers => redact body lines
                    if (!_redactionLineWritten)
                    {
                        WriteLine("C: [REDACTED BODY]");
                        _redactionLineWritten = true;
                    }

                    // Skip logging body lines
                    return;
                }

                // Normal client logging (non-DATA)
                WriteLine($"C: {line}");
                return;
            }
            else
            {
                // Server line handling (needed to detect 354 which enters DATA mode)
                WriteLine($"S: {line}");

                // Enter DATA mode when we see server 354 after DATA
                if (_sawDataCommand && !_inData && line.StartsWith("354", StringComparison.Ordinal))
                {
                    EnterDataMode();
                }

                return;
            }
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