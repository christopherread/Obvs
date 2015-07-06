using System.IO;

namespace Obvs.Serialization
{
    public interface IMessageDeserializer<out TMessage>
        where TMessage : class
    {
        TMessage Deserialize(object obj);
        TMessage Deserialize(Stream source);
        string GetTypeName();
    }

    public abstract class MessageDeserializerBase<TMessage> : IMessageDeserializer<TMessage>
        where TMessage : class
    {
        public abstract TMessage Deserialize(object obj);
        public abstract TMessage Deserialize(Stream source);

        public string GetTypeName()
        {
            return typeof(TMessage).Name;
        }
    }
}