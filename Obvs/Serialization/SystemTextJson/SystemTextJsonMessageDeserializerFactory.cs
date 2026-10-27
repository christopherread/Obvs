using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Obvs.Configuration;

namespace Obvs.Serialization.SystemTextJson
{
    public class SystemTextJsonMessageDeserializerFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
    {
        private readonly JsonSerializerOptions _options;

        /// <param name="options">Defaults to <see cref="SystemTextJsonMessageDefaults.Options"/></param>
        public SystemTextJsonMessageDeserializerFactory(JsonSerializerOptions options = null)
        {
            _options = options ?? SystemTextJsonMessageDefaults.Options;
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
            return (IMessageDeserializer<TMessage>)Activator.CreateInstance(
                typeof(SystemTextJsonMessageDeserializer<>).MakeGenericType(messageType), _options);
        }
    }
}
