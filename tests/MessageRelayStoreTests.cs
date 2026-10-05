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
        public void GetRelayFailureResponse_ReturnsTemporaryFailure_ForUpstream451()
        {
            var exception = new SmtpCommandException(
                SmtpErrorCode.SenderNotAccepted,
                (SmtpStatusCode)451,
                "Temporary server error");

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.Aborted, response.ReplyCode);
            Assert.Equal("Upstream SMTP temporary failure (451)", response.Message);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsPermanentFailure_ForUpstream503()
        {
            var exception = new SmtpCommandException(
                SmtpErrorCode.SenderNotAccepted,
                (SmtpStatusCode)503,
                "Need mail command");

            var response = MessageRelayStore.GetRelayFailureResponse(exception);

            Assert.Equal(SmtpReplyCode.TransactionFailed, response.ReplyCode);
        }

        [Fact]
        public void GetRelayFailureResponse_ReturnsPermanentFailure_ForOtherErrors()
        {
            var response = MessageRelayStore.GetRelayFailureResponse(new IOException("Relay failed"));

            Assert.Equal(SmtpReplyCode.TransactionFailed, response.ReplyCode);
        }
    }
}
