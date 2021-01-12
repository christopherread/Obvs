using Obvs.Logging.NLog;
using Xunit;

namespace Obvs.Logging.Tests
{
    public class TestLoggerWrapper
    {
        [Fact]
        public void ShouldUsesCorrectNameForNLog()
        {
            MyClass myClass = new MyClass(new NLogLoggerFactory());
            myClass.LogSomething();
        }
    }
}
