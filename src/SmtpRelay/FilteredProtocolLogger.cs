using System;
using Serilog;
using SmtpServer.Protocol;

namespace SmtpRelay
{
    public class FilteredProtocolLogger : IProtocolLogger
    {
        // Helper: forward every line into Serilog under SmtpServer context
        void LogLine(string text) =>
            Log.ForContext("SourceContext", "SmtpServer.Protocol")
               .Information(text);

        public void LogConnect(Uri uri) =>
            LogLine($"C: CONNECT {uri}");

        public void LogCommand(byte[] buffer, int offset, int count) =>
            LogLine("C: " + System.Text.Encoding.UTF8.GetString(buffer, offset, count));

        public void LogResponse(byte[] buffer, int offset, int count) =>
            LogLine("S: " + System.Text.Encoding.UTF8.GetString(buffer, offset, count));
    }
}
