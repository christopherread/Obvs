using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessagePack;
using MessagePack.Resolvers;
using Obvs.Configuration;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageDeserializerFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
    {
        private readonly IFormatterResolver _resolver;

        public MessagePackCSharpMessageDeserializerFactory()
            : this(null)
        {
        }

        public MessagePackCSharpMessageDeserializerFactory(IFormatterResolver resolver)
        {
            _resolver = resolver ?? MessagePackSerializer.DefaultOptions.Resolver;
        }

        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return MessageTypes.Get<TMessage, TServiceMessage>(assemblyFilter, typeFilter)
                .Select(type => Create<TMessage>(type))
                .ToArray();
        }

        public IMessageDeserializer<TMessage> Create<TMessage>(Type messageType) where TMessage : class
        {
            return (IMessageDeserializer<TMessage>)Activator.CreateInstance(typeof(MessagePackCSharpMessageDeserializer<>).MakeGenericType(messageType), _resolver);
        }
    }
}