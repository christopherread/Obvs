using System.Collections.Generic;
using System.Linq;

// v6's monitoring, kept for v6 apps and replaced in v7 by OpenTelemetry: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Monitoring
{
    public class MultiMonitorFactory<TMessage> : IMonitorFactory<TMessage>
    {
        private readonly IList<IMonitorFactory<TMessage>> _factories;

        public MultiMonitorFactory(IList<IMonitorFactory<TMessage>> factories)
        {
            _factories = factories;
        }

        public IMonitor<TMessage> Create(string name)
        {
            return new MultiMonitor<TMessage>(_factories.Select(f => f.Create(name)).ToList());
        }
    }
}