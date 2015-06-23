using System;

namespace Obvs.Logging
{
    public class ConsoleLogger : ILogger
    {
        private readonly string _name;

        public ConsoleLogger(string name)
        {
            _name = name;
        }

        public void Debug(string message, Exception exception = null)
        {
            Log(LogLevel.Debug, message, exception);
        }

        public void Info(string message, Exception exception = null)
        {
            Log(LogLevel.Info, message, exception);
        }

        public void Warn(string message, Exception exception = null)
        {
            Log(LogLevel.Warn, message, exception);
        }

        public void Error(string message, Exception exception = null)
        {
            Log(LogLevel.Error, message, exception);
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            WriteToConsole(level.ToString().ToUpper(), message, exception);
        }

        private void WriteToConsole(string logLevel, string message, Exception exception)
        {
            Console.WriteLine("{0} {1} [{2}] {3} {4}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), logLevel, _name, message, exception == null ? "" : exception.ToString());
        }
    }
}