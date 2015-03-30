using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    public class TypeRoutingMessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : IMessage
    {
        private readonly IEnumerable<KeyValuePair<Type, IMessagePublisher<TMessage>>> _publishers;

        public TypeRoutingMessagePublisher(IEnumerable<KeyValuePair<Type, IMessagePublisher<TMessage>>> publishers)
        {
            _publishers = publishers;
        }

        public Task PublishAsync(TMessage message)
        {
            return Task.WhenAll(_publishers.Where(pair => pair.Key.IsInstanceOfType(message))
                .Select(pair => pair.Value)
                .Select(publisher => publisher.PublishAsync(message)));
        }

        public void Dispose()
        {
        }
    }
}