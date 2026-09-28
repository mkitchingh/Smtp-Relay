using System;
using System.IO;
using System.Threading.Tasks;
using SmtpRelay;
using SmtpServer.Protocol;
using Xunit;

namespace Tests
{
    public class MessageRelayStoreTests
    {
        [Fact]
        public void GetRelayFailureResponseReturnsTemporaryFailureForWrappedTimeout()
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
        public void GetRelayFailureResponseReturnsPermanentFailureForOtherErrors()
        {
            var response = MessageRelayStore.GetRelayFailureResponse(new IOException("Relay failed"));

            Assert.Equal(SmtpReplyCode.TransactionFailed, response.ReplyCode);
        }
    }
}
