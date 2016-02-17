using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class GzippedJsonMessageSerializer : JsonMessageSerializer
    {
        public GzippedJsonMessageSerializer(JsonSerializerSettings serializerSettings)
            : base(serializerSettings)
        {
        }

        public override void Serialize(Stream stream, object message)
        {
            using (var gs = new GZipStream(stream, CompressionLevel.Fastest, true))
            {
                base.Serialize(gs, message);
            }
        }
    }
}