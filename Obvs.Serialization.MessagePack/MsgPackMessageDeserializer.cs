using System.IO;
using MsgPack.Serialization;

namespace Obvs.Serialization.MessagePack
{
    public class MsgPackMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
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
            var serializer = SerializationContext.Default.GetSerializer<TMessage>();
            return serializer.Unpack(source);
        }
    }
}