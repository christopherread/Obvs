using System;
using NLog;

namespace Obvs.Logging.NLog
{
    public class NLogLoggerWrapper : ILogger
    {
        private readonly Logger _logger;

        public NLogLoggerWrapper(Logger logger)
        {
            _logger = logger;
        }

        public void Debug(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _logger.Debug(message);
            }
            else
            {
                _logger.Debug(exception, message);
            }
        }

        public void Info(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _logger.Info(message);
            }
            else
            {
                _logger.Info(exception, message);
            }
        }

        public void Warn(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _logger.Warn(message);
            }
            else
            {
                _logger.Warn(exception, message);
            }
        }

        public void Error(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _logger.Error(message);
            }
            else
            {
                _logger.Error(exception, message);
            }
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    Debug(message, exception);
                    break;
                case LogLevel.Info:
                    Info(message, exception);
                    break;
                case LogLevel.Warn:
                    Warn(message, exception);
                    break;
                case LogLevel.Error:
                    Error(message, exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }
    }
}
