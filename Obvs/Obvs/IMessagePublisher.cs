using System;
using Obvs.Types;

namespace Obvs
{
    public interface IMessagePublisher<in TMessage> : IDisposable
        where TMessage : IMessage
    {
        void Publish(TMessage message);
    }

    public class DefaultMessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : IMessage
    {
        public void Publish(TMessage message)
        {
        }

        public void Dispose()
        {
        }
    }
}