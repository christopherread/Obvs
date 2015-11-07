using System;
using System.Reactive.Concurrency;

namespace Obvs.Monitoring
{
    public class ConsoleTimerMonitorFactory<TMessage> : IMonitorFactory<TMessage>
    {
        private readonly TimeSpan _period;
        private readonly IScheduler _scheduler;

        public ConsoleTimerMonitorFactory(TimeSpan period, IScheduler scheduler)
        {
            _period = period;
            _scheduler = scheduler;
        }

        public IMonitor<TMessage> Create(string name)
        {
            return new ConsoleTimerMonitor<TMessage>(name, _period, _scheduler);
        }
    }
}