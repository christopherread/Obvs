using System;
using System.IO;
using MsgPack.Serialization;

namespace Obvs.Serialization.MessagePack
{
    [Obsolete("MsgPack.Cli is no longer maintained; use Obvs.Serialization.MessagePack-CSharp instead")]
    public class MsgPackMessageSerializer : IMessageSerializer
    {
        public void Serialize(Stream destination, object message)
        {
            var serializer = SerializationContext.Default.GetSerializer(message.GetType());
            serializer.Pack(destination, message);
        }
    }
}
