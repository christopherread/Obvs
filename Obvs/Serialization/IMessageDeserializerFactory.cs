using System;
using System.Collections.Generic;
using System.Reflection;

namespace Obvs.Serialization
{
    public interface IMessageDeserializerFactory
    {
        IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class;
    }
}