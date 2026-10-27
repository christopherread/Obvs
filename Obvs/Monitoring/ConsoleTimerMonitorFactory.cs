using System;
using System.Reactive.Concurrency;

// v6's monitoring, kept for v6 apps and replaced in v7 by OpenTelemetry: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

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