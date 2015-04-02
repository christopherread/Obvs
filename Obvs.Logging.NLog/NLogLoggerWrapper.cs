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
                _logger.DebugException(message, exception);
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
                _logger.InfoException(message, exception);
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
                _logger.WarnException(message, exception);
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
                _logger.ErrorException(message, exception);
            }
        }
    }
}
