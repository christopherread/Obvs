using System.IO;
using ProtoBuf;

namespace Obvs.Serialization
{
    public class ProtoBufMessageDeserializer<TMessage> : DeserializerBase<TMessage>, IMessageDeserializer<TMessage>
    {
        public TMessage Deserialize(object obj)
        {
            byte[] data = obj == null ? new byte[0] : (byte[])obj;

            using (MemoryStream stream = new MemoryStream(data))
            {
                return (TMessage)Serializer.NonGeneric.Deserialize(typeof(TMessage), stream);
            }
        }
    }
}