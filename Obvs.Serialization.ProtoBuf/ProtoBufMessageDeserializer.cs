using System.IO;
using ProtoBuf;

namespace Obvs.Serialization.ProtoBuf
{
    public class ProtoBufMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        public override TMessage Deserialize(Stream source)
        {
            return Serializer.Deserialize<TMessage>(source);
        }
    }
}