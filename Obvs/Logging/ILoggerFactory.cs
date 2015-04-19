namespace Obvs.Logging
{
    public interface ILoggerFactory
    {
        ILogger Create(string name);
        ILogger Create<T>();
    }
}