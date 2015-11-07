using System;

namespace Obvs.Monitoring
{
    public interface IMonitor<in TMessage> : IDisposable
    {
        void MessageSent(TMessage message, TimeSpan elapsed);
        void MessageReceived(TMessage message, TimeSpan elapsed);
    }
}