using System;
using System.IO;
using MsgPack.Serialization;

namespace Obvs.Serialization.MessagePack
{
    [Obsolete("MsgPack.Cli is no longer maintained; use Obvs.Serialization.MessagePack-CSharp instead")]
    public class MsgPackMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        public override TMessage Deserialize(Stream source)
        {
            var serializer = SerializationContext.Default.GetSerializer<TMessage>();
            return serializer.Unpack(source);
        }
    }
}