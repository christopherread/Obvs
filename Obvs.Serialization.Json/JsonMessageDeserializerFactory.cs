using System;
using System.Collections.Generic;
using System.Linq;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializerFactory : IMessageDeserializerFactory
    {
        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(string assemblyNameContains = null)
            where TMessage : IMessage
            where TServiceMessage : IMessage
        {
            return MessageTypes.Get<TMessage, TServiceMessage>(assemblyNameContains)
                .Select(type => typeof(JsonMessageDeserializer<>).MakeGenericType(new[] { type }))
                .Select(deserializerGeneric => Activator.CreateInstance(deserializerGeneric) as IMessageDeserializer<TMessage>)
                .ToArray();
        }
    }
}