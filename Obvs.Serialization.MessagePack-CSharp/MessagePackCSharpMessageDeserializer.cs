using System.IO;
using MessagePack;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        private readonly IFormatterResolver _resolver;

        public MessagePackCSharpMessageDeserializer(IFormatterResolver resolver)
        {
            _resolver = resolver;
        }

        public override TMessage Deserialize(Stream source)
        {
            return MessagePackSerializer.Deserialize<TMessage>(source, _resolver);
        }
    }
}