using System;
using System.Diagnostics;

namespace Obvs.Monitoring.PerformanceCounters
{
    public class PerformanceCounterMonitor<TMessage> : IMonitor<TMessage>
    {
        private readonly PerformanceCounter _counterSent;
        private readonly PerformanceCounter _counterReceived;
        private readonly PerformanceCounter _counterSentRate;
        private readonly PerformanceCounter _counterReceivedRate;
        private readonly PerformanceCounter _counterSentAverage;
        private readonly PerformanceCounter _counterSentAverageBase;
        private readonly PerformanceCounter _counterReceivedAverage;
        private readonly PerformanceCounter _counterReceivedAverageBase;

        private bool _enabled = true;
        
        public PerformanceCounterMonitor(string instanceName)
        {
            try
            {
                _counterSent = new PerformanceCounter(Data.CategoryName, Data.MessagesSent, instanceName);
                _counterSentRate = new PerformanceCounter(Data.CategoryName, Data.MessagesSentRate, instanceName);
                _counterSentAverage = new PerformanceCounter(Data.CategoryName, Data.MessagesSentAverage, instanceName);
                _counterSentAverageBase = new PerformanceCounter(Data.CategoryName, Data.MessagesSentAverageBase, instanceName);

                _counterReceived = new PerformanceCounter(Data.CategoryName, Data.MessagesReceived, instanceName);
                _counterReceivedRate = new PerformanceCounter(Data.CategoryName, Data.MessagesReceivedRate, instanceName);
                _counterReceivedAverage = new PerformanceCounter(Data.CategoryName, Data.MessagesReceivedAverage, instanceName);
                _counterReceivedAverageBase = new PerformanceCounter(Data.CategoryName, Data.MessagesReceivedAverageBase, instanceName);
            }
            catch (Exception exception)
            {
                _enabled = false;
                Debug.WriteLine(exception);
            }
        }

        public void Dispose()
        {
            if (_enabled)
            {
                _counterSent.Dispose();
                _counterSentRate.Dispose();
                _counterSentAverage.Dispose();
                _counterSentAverageBase.Dispose();

                _counterReceived.Dispose();
                _counterReceivedRate.Dispose();
                _counterReceivedAverage.Dispose();
                _counterReceivedAverageBase.Dispose();
            }
        }

        public void MessageSent(TMessage message, TimeSpan elapsed)
        {
            if (_enabled)
            {
                try
                {
                    _counterSent.Increment();
                    _counterSentRate.Increment();
                    _counterSentAverage.IncrementBy(elapsed.Ticks);
                    _counterSentAverageBase.Increment();
                }
                catch (Exception exception)
                {
                    _enabled = false;
                    Debug.WriteLine(exception);
                }
            }
        }

        public void MessageReceived(TMessage message, TimeSpan elapsed)
        {
            if (_enabled)
            {
                try
                {
                    _counterReceived.Increment();
                    _counterReceivedRate.Increment();
                    _counterReceivedAverage.IncrementBy(elapsed.Ticks);
                    _counterReceivedAverageBase.Increment();
                }
                catch (Exception exception)
                {
                    _enabled = false;
                    Debug.WriteLine(exception);
                }
            }
        }
    }
}
