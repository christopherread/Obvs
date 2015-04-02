using NUnit.Framework;

namespace Obvs.Logging.NLog.Tests
{
    [TestFixture]
    public class TestLoggerWrapper
    {
        [Test]
        public void TestLoggerUsesCorrectName()
        {
            MyClass myClass = new MyClass(new NLogLoggerFactory());
            myClass.LogSomethingBaby();
        }
    }

    public class MyClass
    {
        private readonly ILogger _logger;

        public MyClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create<MyClass>();
        }

        public void LogSomethingBaby()
        {
            _logger.Debug("Debug Message");
            _logger.Info("Info Message");
            _logger.Warn("Warn Message");
            _logger.Error("Error Message");
        }
    }
}
