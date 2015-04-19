namespace Obvs.Logging
{
    public class ConsoleLoggerFactory : ILoggerFactory
    {
        public ILogger Create(string name)
        {
            return new ConsoleLogger(name);
        }

        public ILogger Create<T>()
        {
            return Create(typeof (T).FullName);
        }
    }
}