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
            WriteToConsole("DEBUG", message, exception);
        }

        public void Info(string message, Exception exception = null)
        {
            WriteToConsole("INFO", message, exception);
        }

        public void Warn(string message, Exception exception = null)
        {
            WriteToConsole("WARN", message, exception);
        }

        public void Error(string message, Exception exception = null)
        {
            WriteToConsole("ERROR", message, exception);
        }

        private void WriteToConsole(string messageType, string message, Exception exception)
        {
            Console.WriteLine("{0} {1} {2} {3} {4}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), messageType, _name, message, exception == null ? "" : exception.ToString());
        }
    }
}