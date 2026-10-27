using System.IO;
using MessagePack;
using MessagePack.Resolvers;

namespace Obvs.Serialization.MessagePack
{
    public class MessagePackCSharpMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackCSharpMessageDeserializer()
            : this(null)
        {
        }

        public MessagePackCSharpMessageDeserializer(IFormatterResolver resolver)
        {
            var options = resolver == null
                ? MessagePackSerializer.DefaultOptions
                : MessagePackSerializer.DefaultOptions.WithResolver(resolver);

            // Messages arrive from other processes, so guard against hostile payloads (deep nesting, hash collisions)
            _options = options.WithSecurity(MessagePackSecurity.UntrustedData);
        }

        public override TMessage Deserialize(Stream source)
        {
            return MessagePackSerializer.Deserialize<TMessage>(source, _options);
        }
    }
}