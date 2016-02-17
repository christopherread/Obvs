using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
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