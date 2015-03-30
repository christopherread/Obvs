using System;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    public interface IMessagePublisher<in TMessage> : IDisposable
        where TMessage : IMessage
    {
        Task PublishAsync(TMessage message);
    }

    public class DefaultMessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : IMessage
    {
        public Task PublishAsync(TMessage message)
        {
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }
    }
}