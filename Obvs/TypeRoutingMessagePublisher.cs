using System;
using System.Collections.Generic;
using System.Linq;
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

        public void Publish(TMessage message)
        {
            IEnumerable<IMessagePublisher<TMessage>> publishers = _publishers.Where(pair => pair.Key.IsInstanceOfType(message))
                                                                             .Select(pair => pair.Value)
                                                                             .ToArray();

            foreach (IMessagePublisher<TMessage> messagePublisher in publishers)
            {
                messagePublisher.Publish(message);
            }
        }

        public void Dispose()
        {
        }
    }
}