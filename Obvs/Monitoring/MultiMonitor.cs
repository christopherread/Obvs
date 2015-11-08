using System;
using System.Collections.Generic;

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