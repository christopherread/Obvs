using System.IO;
using System.IO.Compression;

namespace Obvs.Serialization.NetJson
{
    public class GzippedNetJsonMessageSerializer : NetJsonMessageSerializer
    {

        public override void Serialize(Stream stream, object message)
        {
            using (var gs = new GZipStream(stream, CompressionLevel.Fastest, true))
            {
                using (var streamWriter = new StreamWriter(gs))
                {
                    SerializeCore(streamWriter, message);

                    streamWriter.Flush();
                }
            }
        }
    }
}