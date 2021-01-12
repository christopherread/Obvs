using System.IO;
using System.IO.Compression;

namespace Obvs.Serialization.NetJson
{
    public class GzippedNetJsonMessageDeserializer<TMessage> : NetJsonMessageDeserializer<TMessage>
        where TMessage : class
    {
        public override TMessage Deserialize(Stream stream)
        {
            using (var gs = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                return DeserializeCore(gs);
            }
        }
    }
}