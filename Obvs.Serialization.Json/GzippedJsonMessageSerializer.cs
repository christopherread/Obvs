using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    [Obsolete("Call Compressed() on the service inside AddObvs instead. It gzips whichever serializer the service uses, writes the same bytes as this, and limits how far a message may decompress.")]
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