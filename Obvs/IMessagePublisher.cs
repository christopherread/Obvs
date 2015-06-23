using System;
using System.Threading.Tasks;

namespace Obvs
{
    public interface IMessagePublisher<in TMessage> : IDisposable
        where TMessage : class
    {
        Task PublishAsync(TMessage message);
    }

    public class DefaultMessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
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