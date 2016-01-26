using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class GzippedJsonMessageDeserializer<TMessage> : JsonMessageDeserializer<TMessage>
        where TMessage : class
    {
        public override TMessage Deserialize(object obj)
        {
            using (var ms = new MemoryStream((byte[])obj))
            {
                return Deserialize(ms);
            }
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