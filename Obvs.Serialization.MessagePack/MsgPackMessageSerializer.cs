using System.IO;
using MsgPack.Serialization;

namespace Obvs.Serialization.MessagePack
{
    public class MsgPackMessageSerializer : IMessageSerializer
    {
        public void Serialize(Stream destination, object message)
        {
            var serializer = SerializationContext.Default.GetSerializer(message.GetType());
            serializer.Pack(destination, message);
        }
    }
}
