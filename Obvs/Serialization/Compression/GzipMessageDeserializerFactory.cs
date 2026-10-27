using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace Obvs.Serialization.Compression
{
    /// <summary>
    /// Creates deserializers that gunzip a message before another factory's deserializers read it, and refuse one that
    /// decompresses to more than a limit, so a small message can't expand to fill memory.
    /// </summary>
    public sealed class GzipMessageDeserializerFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
    {
        private readonly IMessageDeserializerFactory _inner;
        private readonly long _maxDecompressedBytes;
        private readonly string _serviceName;

        /// <param name="serviceName">The service the messages belong to, for the error a message over the limit gets</param>
        public GzipMessageDeserializerFactory(IMessageDeserializerFactory inner, long maxDecompressedBytes, string serviceName)
        {
            if (maxDecompressedBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDecompressedBytes), maxDecompressedBytes, "The limit must be at least 1 byte.");
            }
            _inner = inner;
            _maxDecompressedBytes = maxDecompressedBytes;
            _serviceName = serviceName;
        }

        public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            return _inner.Create<TMessage, TServiceMessage>(assemblyFilter, typeFilter).Select(Wrap).ToArray();
        }

        public IMessageDeserializer<TMessage> Create<TMessage>(Type messageType) where TMessage : class
        {
            var deserializer = _inner is IMessageTypeDeserializerFactory typed
                ? typed.Create<TMessage>(messageType)
                : _inner.Create<TMessage, TMessage>(typeFilter: type => type == messageType).Single();
            return Wrap(deserializer);
        }

        private IMessageDeserializer<TMessage> Wrap<TMessage>(IMessageDeserializer<TMessage> inner) where TMessage : class
        {
            return new GzipMessageDeserializer<TMessage>(inner, _maxDecompressedBytes, _serviceName);
        }

        private sealed class GzipMessageDeserializer<TMessage> : IMessageDeserializer<TMessage> where TMessage : class
        {
            private readonly IMessageDeserializer<TMessage> _inner;
            private readonly long _maxDecompressedBytes;
            private readonly string _serviceName;

            public GzipMessageDeserializer(IMessageDeserializer<TMessage> inner, long maxDecompressedBytes, string serviceName)
            {
                _inner = inner;
                _maxDecompressedBytes = maxDecompressedBytes;
                _serviceName = serviceName;
            }

            public TMessage Deserialize(Stream source)
            {
                // Decompressed into memory first, so the inner deserializer never reads past the limit
                using (var gzip = new GZipStream(source, CompressionMode.Decompress, true))
                using (var decompressed = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (decompressed.Length + read > _maxDecompressedBytes)
                        {
                            throw new InvalidDataException(
                                $"A {GetTypeName()} message for service '{_serviceName}' decompresses to more than the limit of {_maxDecompressedBytes} bytes, " +
                                "so it wasn't read. If messages this large are expected, raise maxDecompressedBytes in Compressed(...) on the service.");
                        }
                        decompressed.Write(buffer, 0, read);
                    }
                    decompressed.Position = 0;
                    return _inner.Deserialize(decompressed);
                }
            }

            public string GetTypeName() => _inner.GetTypeName();
        }
    }
}
