using System;
using System.Collections.Generic;
using System.Reflection;

namespace Obvs.Serialization
{
    /// <summary>Creates the deserializers a transport needs for a service's messages.</summary>
    public interface IMessageDeserializerFactory
    {
        /// <summary>A deserializer for each type that's a <typeparamref name="TMessage"/> and a <typeparamref name="TServiceMessage"/>.</summary>
        /// <param name="assemblyFilter">Which assemblies to look in, or all</param>
        /// <param name="typeFilter">Which types to include, or all</param>
        IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class;
    }
}