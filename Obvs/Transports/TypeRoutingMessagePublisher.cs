using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Obvs.Transports
{
    /// <summary>Publishes each message with the publishers registered for its type.</summary>
    public class TypeRoutingMessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly IEnumerable<KeyValuePair<Type, IMessagePublisher<TMessage>>> _publishers;

        /// <summary>Routes to <paramref name="publishers"/>, by type.</summary>
        /// <param name="publishers">Each type and its publisher; a message goes to every publisher whose type it is</param>
        public TypeRoutingMessagePublisher(IEnumerable<KeyValuePair<Type, IMessagePublisher<TMessage>>> publishers)
        {
            _publishers = publishers;
        }

        /// <inheritdoc />
        public Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(_publishers.Where(pair => pair.Key.GetTypeInfo().IsInstanceOfType(message))
                .Select(pair => pair.Value)
                .Select(publisher => publisher.PublishAsync(message, headers, cancellationToken)));
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}