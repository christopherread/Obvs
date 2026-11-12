using System;

namespace Obvs.Serialization
{
    /// <summary>
    /// A deserializer factory that can create a deserializer for a given message type. Services that register their
    /// messages with <c>AddMessages(...)</c> then get exactly those deserializers, without scanning assemblies.
    /// Factories without it are still used, with their scan limited to the registered types.
    /// </summary>
    public interface IMessageTypeDeserializerFactory
    {
        /// <summary>Creates a deserializer for <paramref name="messageType"/>, which is a <typeparamref name="TMessage"/>.</summary>
        IMessageDeserializer<TMessage> Create<TMessage>(Type messageType) where TMessage : class;
    }
}
