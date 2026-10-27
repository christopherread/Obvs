using System.IO;
using System.IO.Compression;

namespace Obvs.Serialization.Compression
{
    /// <summary>
    /// Gzips what another serializer writes. With Obvs.Serialization.Json's <c>JsonMessageSerializer</c> and
    /// <see cref="CompressionLevel.Fastest"/>, it writes the same bytes as v6's <c>GzippedJsonMessageSerializer</c>.
    /// Based on #36 by @fernando-almeida.
    /// </summary>
    public sealed class GzipMessageSerializer : IMessageSerializer
    {
        private readonly CompressionLevel _level;

        public GzipMessageSerializer(IMessageSerializer inner, CompressionLevel level = CompressionLevel.Fastest)
        {
            Inner = inner;
            _level = level;
        }

        /// <summary>The serializer whose output this compresses.</summary>
        public IMessageSerializer Inner { get; }

        public void Serialize(Stream stream, object message)
        {
            using (var gzip = new GZipStream(stream, _level, true))
            {
                Inner.Serialize(gzip, message);
            }
        }
    }
}
