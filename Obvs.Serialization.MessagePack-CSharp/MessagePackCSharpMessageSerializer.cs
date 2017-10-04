using System.IO;
using MessagePack;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageSerializer : IMessageSerializer
    {
        private readonly IFormatterResolver _resolver;

        public MessagePackCSharpMessageSerializer()
            : this(null)
        {
        }

        public MessagePackCSharpMessageSerializer(IFormatterResolver resolver)
        {
            _resolver = resolver ?? MessagePackSerializer.DefaultResolver;
        }

        public void Serialize(Stream destination, object message)
        {
            MessagePackSerializer.NonGeneric.Serialize(message.GetType(), destination, message, _resolver);
        }
    }
}
