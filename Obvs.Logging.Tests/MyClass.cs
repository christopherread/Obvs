namespace Obvs.Logging.Tests
{
    public class MyClass
    {
        private readonly ILogger _logger;

        public MyClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create<MyClass>();
        }

        public void LogSomething()
        {
            _logger.Debug("Debug Message");
            _logger.Info("Info Message");
            _logger.Warn("Warn Message");
            _logger.Error("Error Message");
            _logger.Log(LogLevel.Warn, "Error Message");
        }
    }
}