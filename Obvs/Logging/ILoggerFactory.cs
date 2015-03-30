namespace Obvs.Logging
{
    public interface ILoggerFactory
    {
        ILogger Create(string name);
        ILogger Create<T>();
    }

    public class DebugLoggerFactory : ILoggerFactory
    {
        public ILogger Create(string name)
        {
            return new DebugLogger(name);
        }

        public ILogger Create<T>()
        {
            return Create(typeof (T).FullName);
        }
    }
}