using System.IO;
using ProtoBuf;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageSerializer : IMessageSerializer
    {
        public void Serialize(Stream destination, object message)
        {
            Serializer.NonGeneric.Serialize(destination, message);
        }
    }
}