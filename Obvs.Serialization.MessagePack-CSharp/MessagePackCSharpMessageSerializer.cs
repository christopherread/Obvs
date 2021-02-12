using System.IO;
using MessagePack;
using MessagePack.Resolvers;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageSerializer : IMessageSerializer
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackCSharpMessageSerializer()
            : this(null)
        {
        }

        public MessagePackCSharpMessageSerializer(MessagePackSerializerOptions options)
        {
            _options = options ?? MessagePackSerializerOptions.Standard;
        }

        public void Serialize(Stream destination, object message)
        {
            MessagePackSerializer.Typeless.Serialize(destination, message, _options);
        }
    }
}
