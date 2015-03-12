using System.IO;
using ProtoBuf;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
    {
        public override TMessage Deserialize(object obj)
        {
            byte[] data = obj == null ? new byte[0] : (byte[])obj;

            using (MemoryStream stream = new MemoryStream(data))
            {
                return (TMessage)Serializer.NonGeneric.Deserialize(typeof(TMessage), stream);
            }
        }
    }
}