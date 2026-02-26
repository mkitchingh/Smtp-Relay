using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MailKit;

namespace SmtpRelay
{
    internal sealed class RedactingSmtpProtocolLogger : IProtocolLogger, IDisposable
    {
        private sealed class DummyDetector : IAuthenticationSecretDetector
        {
            public bool IsSecret(string text) => false;

            public IList<AuthenticationSecret> DetectSecrets(byte[] buffer, int offset, int count)
            {
                return Array.Empty<AuthenticationSecret>();
            }
        }

        private readonly object _lock = new();
        private StreamWriter? _writer;
        private bool _disposed;

        private readonly StringBuilder _clientBuf = new();
        private readonly StringBuilder _serverBuf = new();

        private bool _sawData;
        private bool _inData;
        private bool _pastBlankLine;
        private bool _wroteBodyRedaction;

        public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; } = new DummyDetector();

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
        }

        public void LogConnect(Uri uri) => WriteLine($"CONNECT {uri}");

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

        public void LogDisconnect(Uri uri) => WriteLine($"DISCONNECT {uri}");

        private void ProcessChunk(string chunk, bool isClient)
        {
            var sb = isClient ? _clientBuf : _serverBuf;
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
                if (!_inData && line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    _sawData = true;
                    WriteLine("C: DATA");
                    return;
                }

                if (_inData)
                {
                    if (line == ".")
                    {
                        WriteLine("C: .");
                        ExitData();
                        return;
                    }

                    // headers until blank line
                    if (!_pastBlankLine)
                    {
                        if (line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
                            WriteLine("C: Subject: [REDACTED]");
                        else
                            WriteLine("C: " + line);

                        if (line.Length == 0)
                            _pastBlankLine = true;

                        return;
                    }

                    // body redaction
                    if (!_wroteBodyRedaction)
                    {
                        WriteLine("C: [REDACTED BODY]");
                        _wroteBodyRedaction = true;
                    }

                    return;
                }

                WriteLine("C: " + line);
                return;
            }

            // server
            WriteLine("S: " + line);

            // enter DATA mode after server 354
            if (_sawData && !_inData && line.StartsWith("354", StringComparison.Ordinal))
                EnterData();
        }

        private void EnterData()
        {
            _inData = true;
            _sawData = false;
            _pastBlankLine = false;
            _wroteBodyRedaction = false;
        }

        private void ExitData()
        {
            _inData = false;
            _sawData = false;
            _pastBlankLine = false;
            _wroteBodyRedaction = false;
        }

        private void WriteLine(string msg)
        {
            lock (_lock)
            {
                if (_disposed || _writer == null) return;
                _writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                try
                {
                    _writer?.Flush();
                }
                catch
                {
                    // ignore: may already be closed by MailKit
                }

                try
                {
                    _writer?.Dispose();
                }
                catch
                {
                    // ignore: idempotent dispose
                }

                _writer = null;
            }
        }
    }
}