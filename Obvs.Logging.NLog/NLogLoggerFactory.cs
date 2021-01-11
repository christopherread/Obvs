using NLog;

namespace Obvs.Logging.NLog
{
    public class NLogLoggerFactory : ILoggerFactory
    {
        public ILogger Create(string name)
        {
            return new NLogLoggerWrapper(LogManager.GetLogger(name));
        }

        public ILogger Create<T>()
        {
            return new NLogLoggerWrapper(LogManager.GetLogger(typeof(T).FullName));
        }
    }
}