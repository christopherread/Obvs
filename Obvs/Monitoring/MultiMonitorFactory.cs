using System.Collections.Generic;
using System.Linq;

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