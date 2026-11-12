using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Obvs.Configuration;
using ProtoBuf.Meta;

namespace Obvs.Serialization.ProtoBuf
{
    /// <summary>Creates protobuf-net deserializers for a service's messages, for <c>SerializedWith</c>.</summary>
    public class ProtoBufMessageDeserializerFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
    {
        private readonly RuntimeTypeModel _model;

        /// <summary>A factory with <paramref name="model"/>.</summary>
        /// <param name="model">protobuf-net's model, or its default</param>
        public ProtoBufMessageDeserializerFactory(RuntimeTypeModel model = null)
        {
            _model = model;
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
            return (IMessageDeserializer<TMessage>)Activator.CreateInstance(typeof(ProtoBufMessageDeserializer<>).MakeGenericType(messageType), _model);
        }
    }
}