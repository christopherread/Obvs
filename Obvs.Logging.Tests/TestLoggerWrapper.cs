using NUnit.Framework;
using Obvs.Logging.NLog;

namespace Obvs.Logging.Tests
{
    [TestFixture]
    public class TestLoggerWrapper
    {
        [Test]
        public void ShouldUsesCorrectNameForNLog()
        {
            MyClass myClass = new MyClass(new NLogLoggerFactory());
            myClass.LogSomething();
        }
    }
}
