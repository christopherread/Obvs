using System;
using System.IO;
using System.IO.Compression;

namespace Obvs.Serialization
{

    /// <summary>
    /// Gzipped message deserializer
    /// </summary>
    /// <typeparam name="TMessage">Type of message</typeparam>
    public class GZippedMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        /// <summary>
        /// Message from stream deserializer functor
        /// </summary>
        private readonly Func<Stream, TMessage> _messageStreamDeserializerFn;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="messageStreamDeserializerFn">Message deserializer fn</param>
        public GZippedMessageDeserializer(Func<Stream, TMessage> messageStreamDeserializerFn)
        {
            if (messageStreamDeserializerFn == null) {
                throw new ArgumentNullException(nameof(messageStreamDeserializerFn));
            }
            _messageStreamDeserializerFn = messageStreamDeserializerFn;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="messageDeserializer">Message deserializer implementation<</param>
        public GZippedMessageDeserializer(IMessageDeserializer<TMessage> messageDeserializer) {
            if (messageDeserializer == null) {
                throw new ArgumentNullException(nameof(messageDeserializer));
            }
            var gzippedMessageDeserializer = messageDeserializer as GZippedMessageDeserializer<TMessage>;
            if (gzippedMessageDeserializer != null) {
                throw new ArgumentException("Invalid message deserializer GZippedMessageDeserializer");
            }
            _messageStreamDeserializerFn = messageDeserializer.Deserialize;
        }

        /// <inheritdoc />
        public override TMessage Deserialize(Stream stream)
        {
            using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                return _messageStreamDeserializerFn(gzipStream);
            }
        }
    }

    public static class GZippedMessageDeserializerExtensions {

        /// <summary>
        /// Apply GZip decompression to an existing message serializer
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        public static GZippedMessageDeserializer<TMessage> DeserializeGZipped<TMessage>(
            this IMessageDeserializer<TMessage> messageDeserializer
        ) where TMessage : class {
            if (messageDeserializer is GZippedMessageDeserializer<TMessage>) {
                throw new ArgumentException("Message serializer implementation cannot be GzippedMessageDeserializer");
            }
            return new GZippedMessageDeserializer<TMessage>(messageDeserializer.Deserialize);
        }
    }
}