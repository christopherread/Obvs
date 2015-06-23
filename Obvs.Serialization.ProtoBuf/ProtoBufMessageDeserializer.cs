using System.IO;
using ProtoBuf;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        public override TMessage Deserialize(object obj)
        {
            byte[] data = obj == null ? new byte[0] : (byte[])obj;

            using (MemoryStream stream = new MemoryStream(data))
            {
                return Deserialize(stream);
            }
        }

        public override TMessage Deserialize(Stream source)
        {
            return Serializer.Deserialize<TMessage>(source);
        }
    }
}