using System;
using System.IO;
using System.IO.Compression;

namespace Obvs.Serialization
{
    /// <summary>
    /// Gzipped message serializer
    /// </summary>
    public class GZippedMessageSerializer : IMessageSerializer
    {

        /// <summary>
        /// Message to strem serializer functor
        /// </summary>
        private readonly Action<Stream, object> _messageStreamSerializerFn;

        /// <summary>
        /// Compression level
        /// </summary>
        private readonly CompressionLevel _compressionLevel;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="messageStreamSerializerFn">Message stream serializer</param>
        /// <param name="compressionLevel">Compression level</param>
        public GZippedMessageSerializer(Action<Stream, object> messageStreamSerializerFn, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (messageStreamSerializerFn == null) {
                throw new ArgumentNullException(nameof(messageStreamSerializerFn));
            }
            _messageStreamSerializerFn = messageStreamSerializerFn;
            _compressionLevel = compressionLevel;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="messageSerializer">Message stream serializer</param>
        /// <param name="compressionLevel">Compression level</param>
        public GZippedMessageSerializer(IMessageSerializer messageSerializer, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (messageSerializer == null) {
                throw new ArgumentNullException(nameof(messageSerializer));
            }
            var gzippedMessageSerializer = messageSerializer as GZippedMessageSerializer;
            if (gzippedMessageSerializer != null) {
                throw new ArgumentException("Invalid message deserializer GZippedMessageSerializer");
            }
            _messageStreamSerializerFn = messageSerializer.Serialize;
            _compressionLevel = compressionLevel;
        }

        /// <inheritdoc />
        public void Serialize(Stream stream, object message)
        {
            using (var gzipStream = new GZipStream(stream, _compressionLevel, true))
            {
                _messageStreamSerializerFn(gzipStream, message);
            }
        }
    }

    public static class GZippedMessageSerializerExtensions {

        /// <summary>
        /// Apply GZip compression to an existing message serializer
        /// </summary>
        /// <typeparam name="TMessage">Type of message</typeparam>
        public static GZippedMessageSerializer SerializeGZipped(this IMessageSerializer messageSerializer) {
            if (messageSerializer is GZippedMessageSerializer) {
                throw new ArgumentException("Message serializer implementation cannot be GzippedMessageSerializer");
            }
            return new GZippedMessageSerializer(messageSerializer.Serialize);
        }
    }
}