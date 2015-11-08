using System;
using System.Diagnostics;

namespace Obvs.Monitoring.PerformanceCounters
{
    public class PerformanceCounterMonitor<TMessage> : IMonitor<TMessage>
    {
        private readonly string _name;
        
        public PerformanceCounterMonitor(string name, CounterCreationDataCollection collection)
        {
            _name = name;

            var numberOfItems = new CounterCreationData
            {
                CounterType = PerformanceCounterType.NumberOfItems64,
                CounterName = _name,
            };

            var rates = new CounterCreationData
            {
                CounterType = PerformanceCounterType.RateOfCountsPerSecond64,
                CounterName = string.Format("{0} msg/sec", _name),
            };
            collection.Add(numberOfItems);
            collection.Add(rates);

            var _numCounter = new PerformanceCounter();
        }

        public void Dispose()
        {
            
        }

        public void MessageSent(TMessage message, TimeSpan elapsed)
        {
           
        }

        public void MessageReceived(TMessage message, TimeSpan elapsed)
        {
            
        }
    }
}
