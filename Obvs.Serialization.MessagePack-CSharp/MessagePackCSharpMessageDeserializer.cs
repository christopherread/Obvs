using System.IO;
using MessagePack;
using MessagePack.Resolvers;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        private readonly IFormatterResolver _resolver;

        public MessagePackCSharpMessageDeserializer()
            : this(null)
        {
        }

        public MessagePackCSharpMessageDeserializer(IFormatterResolver resolver)
        {
            _resolver = resolver ?? MessagePackSerializer.DefaultResolver;
        }

        public override TMessage Deserialize(Stream source)
        {
            return MessagePackSerializer.Deserialize<TMessage>(source, _resolver);
        }
    }
}