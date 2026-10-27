using System.IO;

namespace Obvs.Serialization
{
    /// <summary>Helpers for deserializers.</summary>
    public static class MessageDeserializerExtentions
    {
        /// <summary>Deserializes a message from bytes.</summary>
        public static TMessage Deserialize<TMessage>(this IMessageDeserializer<TMessage> deserializer, byte[] obj)
            where TMessage : class
        {
            using (MemoryStream ms = new MemoryStream(obj))
            {
                return deserializer.Deserialize(ms);
            }
        }
    }

    /// <summary>Reads one type of message.</summary>
    public interface IMessageDeserializer<out TMessage>
        where TMessage : class
    {
        /// <summary>Reads a message from <paramref name="source"/>.</summary>
        TMessage Deserialize(Stream source);

        /// <summary>The name of the type this reads, which matches the type name a message is sent with.</summary>
        string GetTypeName();
    }

    /// <summary>A base for deserializers of <typeparamref name="TMessage"/>, named after it.</summary>
    public abstract class MessageDeserializerBase<TMessage> : IMessageDeserializer<TMessage>
        where TMessage : class
    {
        /// <inheritdoc />
        public abstract TMessage Deserialize(Stream source);

        /// <inheritdoc />
        public string GetTypeName()
        {
            return typeof(TMessage).Name;
        }
    }
}