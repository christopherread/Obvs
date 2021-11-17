using System;
using System.Threading.Tasks;

namespace Obvs
{
    /// <inheritdoc cref="PublishAsync(TMessage)"/>
    public interface IMessagePublisher<in TMessage> : IDisposable
        where TMessage : class
    {
        /// <summary> asynchronously sends <paramref name="message"/> to a single topic. </summary>
        Task PublishAsync(TMessage message);
    }

    /// <summary> Null-Object <see cref="IMessagePublisher{TMessage}"/>; actually does NOT publish </summary>
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