using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Obvs.Serialization;

namespace Obvs.Hosting
{
    // What a service that registers its messages hands its transport: deserializers for the registered types of the
    // kind asked for. A factory that can create one for a given type is asked for each, so nothing is scanned; any
    // other factory scans as before, with the registered types as the only ones it may use.
    internal sealed class RegisteredMessageDeserializerFactory : IMessageDeserializerFactory
    {
        private readonly IMessageDeserializerFactory _inner;
        private readonly IReadOnlyCollection<Type> _messages;

        public RegisteredMessageDeserializerFactory(IMessageDeserializerFactory inner, IReadOnlyCollection<Type> messages)
        {
            _inner = inner;
            _messages = messages;
        }

        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            bool IsWanted(Type type) => typeof(TMessage).IsAssignableFrom(type) && typeof(TServiceMessage).IsAssignableFrom(type) &&
                                        (assemblyFilter?.Invoke(type.Assembly) ?? true) && (typeFilter?.Invoke(type) ?? true);

            if (_inner is IMessageTypeDeserializerFactory typed)
            {
                return _messages.Where(IsWanted).Select(type => typed.Create<TMessage>(type)).ToArray();
            }
            return _inner.Create<TMessage, TServiceMessage>(assemblyFilter, type => _messages.Contains(type) && IsWanted(type));
        }
    }
}
