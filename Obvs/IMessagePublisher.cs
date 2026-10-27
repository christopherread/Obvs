using System;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs
{
    /// <inheritdoc cref="PublishAsync(TMessage, CancellationToken)"/>
    public interface IMessagePublisher<in TMessage> : IDisposable
        where TMessage : class
    {
        /// <summary> asynchronously sends <paramref name="message"/> to a single topic. </summary>
        Task PublishAsync(TMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary> Null-Object <see cref="IMessagePublisher{TMessage}"/>; actually does NOT publish </summary>
    public class DefaultMessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }
    }
}