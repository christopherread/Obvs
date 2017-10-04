using System.IO;
using MessagePack;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        public override TMessage Deserialize(Stream source)
        {
            return MessagePackSerializer.Deserialize<TMessage>(source);
        }
    }
}