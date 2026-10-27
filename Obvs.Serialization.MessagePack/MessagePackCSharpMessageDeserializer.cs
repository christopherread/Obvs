using System.IO;
using MessagePack;
using MessagePack.Resolvers;

namespace Obvs.Serialization.MessagePack
{
    /// <summary>Reads a <typeparamref name="TMessage"/> as MessagePack, with MessagePack-CSharp.</summary>
    public class MessagePackCSharpMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage> 
        where TMessage : class
    {
        private readonly MessagePackSerializerOptions _options;

        /// <summary>A deserializer with MessagePack-CSharp's contractless resolver.</summary>
        public MessagePackCSharpMessageDeserializer()
            : this(null)
        {
        }

        /// <summary>A deserializer with <paramref name="resolver"/>.</summary>
        /// <param name="resolver">How MessagePack-CSharp finds formatters</param>
        public MessagePackCSharpMessageDeserializer(IFormatterResolver resolver)
        {
            var options = resolver == null
                ? MessagePackSerializer.DefaultOptions
                : MessagePackSerializer.DefaultOptions.WithResolver(resolver);

            // Messages arrive from other processes, so guard against hostile payloads (deep nesting, hash collisions)
            _options = options.WithSecurity(MessagePackSecurity.UntrustedData);
        }

        /// <inheritdoc />
        public override TMessage Deserialize(Stream source)
        {
            return MessagePackSerializer.Deserialize<TMessage>(source, _options);
        }
    }
}