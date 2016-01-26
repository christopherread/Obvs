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

                return stream.ToArray();
            }
        }

        public void Serialize(Stream destination, object message)
        {
            Serializer.NonGeneric.Serialize(destination, message);
        }
    }
}