using System;
using System.IO;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using SmtpRelay;
using SmtpServer.Protocol;
using Xunit;

namespace Tests
{
    public class MessageRelayStoreTests
    {
        [Fact]
        public void GetRelayFailureResponse_ReturnsTemporaryFailure_ForDirectTimeout()
        {
            var exception = new TimeoutException("Operation timed out");

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.Aborted, response.ReplyCode);
            Assert.Equal("Relay operation timed out", response.Message);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsTemporaryFailure_ForWrappedTimeout()
        {
            var exception = new IOException(
                "The read operation failed, see inner exception.",
                new TimeoutException(
                    "Operation timed out",
                    new TaskCanceledException("A task was canceled.")));

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.Aborted, response.ReplyCode);
            Assert.Equal("Relay operation timed out", response.Message);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsPermanentFailure_ForOtherErrors()
        {
            var response = MessageRelayStore.GetRelayFailureResponse(new IOException("Relay failed"));

            Assert.Equal(SmtpReplyCode.TransactionFailed, response.ReplyCode);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsTemporaryFailure_ForUpstream4xxResponse()
        {
            var exception = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted,
                SmtpStatusCode.ServiceNotAvailable, "Upstream 4xx");

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.Aborted, response.ReplyCode);
            Assert.Contains("421", response.Message);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsTemporaryFailure_ForSocketException()
        {
            var exception = new System.Net.Sockets.SocketException(11001); // No such host is known

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.Aborted, response.ReplyCode);
            Assert.Contains("retry", response.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsTemporaryFailure_ForWrappedSocketException()
        {
            var exception = new IOException(
                "Network error",
                new System.Net.Sockets.SocketException(10061)); // Connection refused

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.Aborted, response.ReplyCode);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsPermanentFailure_WithUpstreamDetails_For5xxResponse()
        {
            var exception = new SmtpCommandException(SmtpErrorCode.MessageNotAccepted,
                SmtpStatusCode.MailboxUnavailable, "5.7.1 Message Rejected");

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.TransactionFailed, response.ReplyCode);
            Assert.Contains("550", response.Message);
            Assert.Contains("5.7.1 Message Rejected", response.Message);
        }
    }
}
