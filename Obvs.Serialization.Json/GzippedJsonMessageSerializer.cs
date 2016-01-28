using System.IO;
using System.IO.Compression;

namespace Obvs.Serialization.Json
{
    public class GzippedJsonMessageSerializer : JsonMessageSerializer
    {
        public override void Serialize(Stream stream, object message)
        {
            using (var gs = new GZipStream(stream, CompressionLevel.Fastest, true))
            {
                base.Serialize(gs, message);
            }
        }
    }
}