using System.IO;
using MessagePack;
using MessagePack.Resolvers;

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
            _resolver = resolver ?? StandardResolver.Instance;
        }

        public void Serialize(Stream destination, object message)
        {
            MessagePackSerializer.NonGeneric.Serialize(message.GetType(), destination, message, _resolver);
        }
    }
}
