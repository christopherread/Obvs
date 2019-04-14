using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Obvs.Configuration;

using YamlDotNet.Serialization;

namespace Obvs.Serialization.Yaml {
    public class YamlMessageDeserializerFactory : IMessageDeserializerFactory {
        private readonly Type _deserializerType;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deserializerType">Type of the deserializer implementation</param>
        public YamlMessageDeserializerFactory(Type deserializerType) {
            _deserializerType = deserializerType;
        }

        /// <inheritdoc >
        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
        where TMessage : class
        where TServiceMessage : class {
            return MessageTypes.Get<TMessage, TServiceMessage>(assemblyFilter, typeFilter)
                .Select(type => _deserializerType.MakeGenericType(type))
                .Select(deserializerGeneric => Activator.CreateInstance(deserializerGeneric) as IMessageDeserializer<TMessage>)
                .ToArray();
        }
    }
}