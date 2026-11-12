using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Obvs.Configuration;

namespace Obvs.Serialization.Json
{
    /// <summary>
    /// Creates Newtonsoft JSON deserializers for a service's messages, for <c>SerializedWith</c>: <c>new
    /// JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer&lt;&gt;))</c>.
    /// </summary>
    public class JsonMessageDeserializerFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
    {
        private readonly Type _deserializerType;
        private readonly JsonSerializerSettings _serializerSettings;

        /// <summary>A factory for deserializers of type <paramref name="deserializerType"/>.</summary>
        /// <param name="deserializerType">The open generic deserializer type, such as <c>typeof(JsonMessageDeserializer&lt;&gt;)</c></param>
        /// <param name="serializerSettings">Newtonsoft's settings, or its defaults</param>
        public JsonMessageDeserializerFactory(Type deserializerType, JsonSerializerSettings serializerSettings = null)
        {
            _serializerSettings = serializerSettings;
            _deserializerType = deserializerType;
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
            return (IMessageDeserializer<TMessage>)Activator.CreateInstance(_deserializerType.MakeGenericType(messageType), _serializerSettings);
        }
    }
}