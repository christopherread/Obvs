using System;
using System.IO;
using MsgPack.Serialization;

namespace Obvs.Serialization.MessagePack
{
    public class MsgPackMessageSerializer : IMessageSerializer
    {
        public object Serialize(object message)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Serialize(stream, message);
                byte[] buffer = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }

        public void Serialize(Stream destination, object message)
        {
            var serializer = SerializationContext.Default.GetSerializer(message.GetType());
            serializer.Pack(destination, message);
        }
    }
}
