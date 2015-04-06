using NUnit.Framework;
using Obvs.Logging.NLog;
using Obvs.Logging.Log4Net;

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
        
        [Test]
        public void ShouldUsesCorrectNameForLog4Net()
        {
            MyClass myClass = new MyClass(new Log4NetLogFactory());
            myClass.LogSomething();
        }
    }
}
