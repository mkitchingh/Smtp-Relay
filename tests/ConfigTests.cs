using SmtpRelay;
using Xunit;

namespace Tests
{
    public class ConfigTests
    {
        [Fact]
        public void RoundTrip()
        {
            var cfg = new Config{SmartHost="smtp.test"};
            cfg.Save();
            var loaded = Config.Load();
            Assert.Equal("smtp.test", loaded.SmartHost);
        }
    }
}
