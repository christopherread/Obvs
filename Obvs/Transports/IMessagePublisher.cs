using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.Transports
{
    /// <inheritdoc cref="PublishAsync(TMessage, IReadOnlyDictionary{string, string}, CancellationToken)"/>
    public interface IMessagePublisher<in TMessage> : IDisposable
        where TMessage : class
    {
        /// <summary>Sends <paramref name="message"/>, with <paramref name="headers"/>, to a single topic.</summary>
        /// <param name="message">The message</param>
        /// <param name="headers">The headers to send with it, which include the trace context</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
    }

    /// <summary> Null-Object <see cref="IMessagePublisher{TMessage}"/>; actually does NOT publish </summary>
    public class DefaultMessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        /// <inheritdoc />
        public Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}