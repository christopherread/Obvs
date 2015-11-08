using System.Diagnostics;

namespace Obvs.Monitoring.PerformanceCounters
{
    public class PerformanceCounterMonitorFactory<TMessage> : IMonitorFactory<TMessage>
    {
        private readonly CounterCreationDataCollection _collection;

        public PerformanceCounterMonitorFactory(CounterCreationDataCollection collection)
        {
            _collection = collection;
        }

        public IMonitor<TMessage> Create(string name)
        {
            return new PerformanceCounterMonitor<TMessage>(name, _collection);
        }
    }
}