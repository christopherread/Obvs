using System.IO;
using MessagePack;

namespace Obvs.Serialization.MessagePack
{
    /// <summary>Writes messages as MessagePack, with MessagePack-CSharp. Use it with <c>SerializedWith</c>.</summary>
    public class MessagePackCSharpMessageSerializer : IMessageSerializer
    {
        private readonly MessagePackSerializerOptions _options;

        /// <summary>A serializer with MessagePack-CSharp's contractless resolver.</summary>
        public MessagePackCSharpMessageSerializer()
            : this(null)
        {
        }

        /// <summary>A serializer with <paramref name="resolver"/>.</summary>
        /// <param name="resolver">How MessagePack-CSharp finds formatters</param>
        public MessagePackCSharpMessageSerializer(IFormatterResolver resolver)
        {
            _options = resolver == null
                ? MessagePackSerializer.DefaultOptions
                : MessagePackSerializer.DefaultOptions.WithResolver(resolver);
        }

        /// <inheritdoc />
        public void Serialize(Stream destination, object message)
        {
            MessagePackSerializer.Serialize(message.GetType(), destination, message, _options);
        }
    }
}
