using System;

namespace Obvs.Logging
{
    public interface ILogger
    {
        void Debug(string message, Exception exception = null);
        void Info(string message, Exception exception = null);
        void Warn(string message, Exception exception = null);
        void Error(string message, Exception exception = null);
        void Log(LogLevel level, string message, Exception exception = null);
    }
}