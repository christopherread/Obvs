using System;
using log4net;

namespace Obvs.Logging.Log4Net
{
    public class Log4NetLogWrapper : ILogger
    {
        private readonly ILog _log;

        public Log4NetLogWrapper(ILog log)
        {
            _log = log;
        }

        public void Debug(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _log.Debug(message);
            }
            else
            {
                _log.Debug(message, exception);
            }
        }

        public void Info(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _log.Info(message);
            }
            else
            {
                _log.Info(message, exception);
            }
        }

        public void Warn(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _log.Warn(message);
            }
            else
            {
                _log.Warn(message, exception);
            }
        }

        public void Error(string message, Exception exception = null)
        {
            if (exception == null)
            {
                _log.Error(message);
            }
            else
            {
                _log.Error(message, exception);
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
                    throw new ArgumentOutOfRangeException("level", level, null);
            }
        }
    }
}
