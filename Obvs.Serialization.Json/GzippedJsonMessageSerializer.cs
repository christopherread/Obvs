using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Obvs.Serialization.Json
{
    public class GzippedJsonMessageSerializer : JsonMessageSerializer
    {
        public override object Serialize(object message)
        {
            using (var ms = new MemoryStream())
            {
                Serialize(ms, message);

                return ms.ToArray();
            }
        }
        //public override object Serialize(object message)
        //{
        //    using (var ms = new MemoryStream())
        //    {
        //        Serialize(ms, message);

        //        return ms.ToArray();
        //    }
        //}

        public override void Serialize(Stream stream, object message)
        {
            using (var gs = new GZipStream(stream, CompressionLevel.Fastest, true))
            {
                base.Serialize(gs, message);
            }
        }
    }
}