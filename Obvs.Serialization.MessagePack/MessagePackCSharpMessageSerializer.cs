using System.IO;
using MessagePack;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageSerializer : IMessageSerializer
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackCSharpMessageSerializer()
            : this(null)
        {
        }

        public MessagePackCSharpMessageSerializer(IFormatterResolver resolver)
        {
            _options = resolver == null
                ? MessagePackSerializer.DefaultOptions
                : MessagePackSerializer.DefaultOptions.WithResolver(resolver);
        }

        public void Serialize(Stream destination, object message)
        {
            MessagePackSerializer.Serialize(message.GetType(), destination, message, _options);
        }
    }
}
