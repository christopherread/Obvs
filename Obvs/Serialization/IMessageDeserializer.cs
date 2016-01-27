using System.IO;

namespace Obvs.Serialization
{
    public static class MessageDeserializerExtentions
    {
        public static TMessage Deserialize<TMessage>(this IMessageDeserializer<TMessage> deserializer, byte[] obj)
            where TMessage : class
        {
            using (MemoryStream ms = new MemoryStream(obj))
            {
                return deserializer.Deserialize(ms);
            }
        }
    }

    public interface IMessageDeserializer<out TMessage>
        where TMessage : class
    {
        TMessage Deserialize(Stream source);

        string GetTypeName();
    }

    public abstract class MessageDeserializerBase<TMessage> : IMessageDeserializer<TMessage>
        where TMessage : class
    {
        public abstract TMessage Deserialize(Stream source);

        public string GetTypeName()
        {
            return typeof(TMessage).Name;
        }
    }
}