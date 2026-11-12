using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessagePack;
using MessagePack.Resolvers;
using Obvs.Configuration;

namespace Obvs.Serialization.MessagePack
{
    /// <summary>Creates MessagePack-CSharp deserializers for a service's messages, for <c>SerializedWith</c>.</summary>
    public class MessagePackCSharpMessageDeserializerFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
    {
        private readonly IFormatterResolver _resolver;

        /// <summary>A factory with MessagePack-CSharp's contractless resolver.</summary>
        public MessagePackCSharpMessageDeserializerFactory()
            : this(null)
        {
        }

        /// <summary>A factory with <paramref name="resolver"/>.</summary>
        /// <param name="resolver">How MessagePack-CSharp finds formatters</param>
        public MessagePackCSharpMessageDeserializerFactory(IFormatterResolver resolver)
        {
            _resolver = resolver ?? MessagePackSerializer.DefaultOptions.Resolver;
        }

        /// <inheritdoc />
        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return MessageTypes.Get<TMessage, TServiceMessage>(assemblyFilter, typeFilter)
                .Select(type => Create<TMessage>(type))
                .ToArray();
        }

        /// <inheritdoc />
        public IMessageDeserializer<TMessage> Create<TMessage>(Type messageType) where TMessage : class
        {
            return (IMessageDeserializer<TMessage>)Activator.CreateInstance(typeof(MessagePackCSharpMessageDeserializer<>).MakeGenericType(messageType), _resolver);
        }
    }
}