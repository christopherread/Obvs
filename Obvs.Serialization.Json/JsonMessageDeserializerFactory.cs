using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Obvs.Configuration;

namespace Obvs.Serialization.Json
{
    public class JsonMessageDeserializerFactory : IMessageDeserializerFactory
    {
        private readonly Type _deserializerType;
        private readonly JsonSerializerSettings _serializerSettings;

        public JsonMessageDeserializerFactory(Type deserializerType, JsonSerializerSettings serializerSettings = null)
        {
            _serializerSettings = serializerSettings;
            _deserializerType = deserializerType;
        }

        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return MessageTypes.Get<TMessage, TServiceMessage>(assemblyFilter, typeFilter)
                .Select(type => _deserializerType.MakeGenericType(type))
                .Select(deserializerGeneric => Activator.CreateInstance(deserializerGeneric, _serializerSettings) as IMessageDeserializer<TMessage>)
                .ToArray();
        }
    }
}