using System.IO;
using ProtoBuf;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageSerializer : IMessageSerializer
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
            Serializer.NonGeneric.Serialize(destination, message);
        }
    }
}