using System.IO;
using ProtoBuf;

namespace Obvs.Serialization
{
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        public object Serialize(object message)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Serializer.NonGeneric.Serialize(stream, message);

                byte[] buffer = new byte[stream.Length];

                stream.Position = 0;

                stream.Read(buffer, 0, buffer.Length);

                return buffer;
            }
        }
    }
}