using log4net;

namespace Obvs.Logging.Log4Net
{
    public class Log4NetLogFactory : ILoggerFactory
    {
        public ILogger Create(string name)
        {
            return new Log4NetLogWrapper(LogManager.GetLogger(name));
        }

        public ILogger Create<T>()
        {
            return new Log4NetLogWrapper(LogManager.GetLogger(typeof(T)));
        }
    }
}