namespace Obvs.Monitoring
{
    public interface IMonitorFactory<in TMessage>
    {
        IMonitor<TMessage> Create(string name);
    }
}