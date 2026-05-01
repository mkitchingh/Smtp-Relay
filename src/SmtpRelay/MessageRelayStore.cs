using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using SmtpResponse = SmtpServer.Protocol.SmtpResponse;

namespace SmtpRelay
{
    public sealed class MessageRelayStore : IMessageStore
    {
        private readonly Config _cfg;
        private readonly ILogger _log;

        public MessageRelayStore(Config cfg, ILogger log)
        {
            _cfg = cfg;
            _log = log;
        }

        public async Task<SmtpResponse> SaveAsync(
            ISessionContext context,
            IMessageTransaction transaction,
            ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            var clientIp = GetClientIp(context) ?? "unknown";

            if (!_cfg.IsIPAllowed(clientIp))
            {
                _log.LogWarning("Rejected relay request from {IP}", clientIp);
                return new SmtpResponse(SmtpReplyCode.MailboxUnavailable, "Relay access denied");
            }

            try
            {
                await using var stream = new MemoryStream();
                foreach (var seg in buffer)
                    stream.Write(seg.Span);

                stream.Position = 0;
                var message = await MimeMessage.LoadAsync(stream, cancellationToken);

                var mode = _cfg.GetEffectiveSecurity();
                var socketOptions = mode switch
                {
                    OutboundSecurityMode.Smtps    => SecureSocketOptions.SslOnConnect,
                    OutboundSecurityMode.StartTls => SecureSocketOptions.StartTls,
                    _                             => SecureSocketOptions.None
                };

                _log.LogInformation(
                    "Connecting to {Host}:{Port} (Security=\"{Security}\", SocketOptions=\"{Options}\")",
                    _cfg.SmartHost,
                    _cfg.SmartHostPort,
                    mode,
                    socketOptions);

                if (_cfg.EnableLogging)
                {
                    Directory.CreateDirectory(Config.SharedLogDir);
                    var protoPath = Path.Combine(Config.SharedLogDir, $"smtp-{DateTime.Now:yyyyMMdd}.log");

                    // IMPORTANT: do not open/append to the file separately.
                    // The protocol logger owns the file handle for the duration of the SMTP session.
                    var proto = new RedactingSmtpProtocolLogger(protoPath, append: true);

                    using var client = new SmtpClient(proto) { Timeout = 15000 };
                    await SendWithClientAsync(client, message, transaction, socketOptions, cancellationToken);
                }
                else
                {
                    using var client = new SmtpClient { Timeout = 15000 };
                    await SendWithClientAsync(client, message, transaction, socketOptions, cancellationToken);
                }

                _log.LogInformation("Relayed mail from {IP}", clientIp);
                return SmtpResponse.Ok;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Relay failure");
                return SmtpResponse.TransactionFailed;
            }
        }

        private async Task SendWithClientAsync(
            SmtpClient client,
            MimeMessage message,
            IMessageTransaction transaction,
            SecureSocketOptions socketOptions,
            CancellationToken cancellationToken)
        {
            await client.ConnectAsync(_cfg.SmartHost, _cfg.SmartHostPort, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_cfg.Username))
                await client.AuthenticateAsync(_cfg.Username, _cfg.Password ?? string.Empty, cancellationToken);

            var senderAddress = $"{transaction.From.User}@{transaction.From.Host}";
            var hasEnvelopeSender = !string.IsNullOrWhiteSpace(transaction.From.User);

            if (hasEnvelopeSender)
            {
                var sender = new MailboxAddress(string.Empty, senderAddress);
                var recipients = new List<MailboxAddress>();
                foreach (var recipient in transaction.To)
                    recipients.Add(new MailboxAddress(string.Empty, $"{recipient.User}@{recipient.Host}"));
                await client.SendAsync(message, sender, recipients, cancellationToken);
            }
            else
            {
                // Empty envelope sender (MAIL FROM:<>) — let MailKit use the MIME headers
                await client.SendAsync(message, cancellationToken);
            }
            await client.DisconnectAsync(true, cancellationToken);
        }

        private static string? GetClientIp(ISessionContext ctx)
        {
            static bool IsReal(IPEndPoint ep) =>
                !ep.Address.Equals(IPAddress.Any) && !ep.Address.Equals(IPAddress.IPv6Any);

            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var p in ctx.GetType().GetProperties(BF))
            {
                if (typeof(EndPoint).IsAssignableFrom(p.PropertyType) &&
                    p.GetValue(ctx) is IPEndPoint ep && IsReal(ep))
                    return ep.Address.ToString();
            }

            if (ctx.Properties.TryGetValue("RemoteEndPoint", out var o1) && o1 is IPEndPoint ep1 && IsReal(ep1))
                return ep1.Address.ToString();

            if (ctx.Properties.TryGetValue("SessionRemoteEndPoint", out var o2) && o2 is IPEndPoint ep2 && IsReal(ep2))
                return ep2.Address.ToString();

            foreach (var v in ctx.Properties.Values)
            {
                if (v is IPEndPoint ep3 && IsReal(ep3))
                    return ep3.Address.ToString();

                if (v is EndPoint ep4 && ep4 is IPEndPoint ipEp && IsReal(ipEp))
                    return ipEp.Address.ToString();

                if (v is string s && IPAddress.TryParse(s, out var ip) && IsReal(new IPEndPoint(ip, 0)))
                    return ip.ToString();
            }

            return null;
        }
    }
}
