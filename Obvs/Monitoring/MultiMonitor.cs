using System;
using System.Collections.Generic;

// v6's monitoring, kept for v6 apps and replaced in v7 by OpenTelemetry: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.Monitoring
{
    public class MultiMonitor<TMessage> : IMonitor<TMessage>
    {
        private readonly IList<IMonitor<TMessage>> _monitors;

        public MultiMonitor(IList<IMonitor<TMessage>> monitors)
        {
            _monitors = monitors;
        }

        public void Dispose()
        {
            foreach (var monitor in _monitors)
            {
                monitor.Dispose();
            }
        }

        public void MessageSent(TMessage message, TimeSpan elapsed)
        {
            foreach (var monitor in _monitors)
            {
                monitor.MessageSent(message, elapsed);
            }
        }

        public void MessageReceived(TMessage message, TimeSpan elapsed)
        {
            foreach (var monitor in _monitors)
            {
                monitor.MessageReceived(message, elapsed);
            }
        }
    }
}