using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    [Obsolete("Call Compressed() on the service inside AddObvs instead. It gzips whichever serializer the service uses, writes the same bytes as this, and limits how far a message may decompress.")]
    public class GzippedJsonMessageDeserializer<TMessage> : JsonMessageDeserializer<TMessage>
        where TMessage : class
    {
        public GzippedJsonMessageDeserializer(JsonSerializerSettings serializerSettings)
            : base(serializerSettings)
        {
        }

        public override TMessage Deserialize(Stream stream)
        {
            using (var gs = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                return base.Deserialize(gs);
            }
        }
    }
}