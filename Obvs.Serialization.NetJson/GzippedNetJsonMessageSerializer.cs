using System.IO;
using System.IO.Compression;
using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public class GzippedNetJsonMessageSerializer : NetJsonMessageSerializer
    {

        public override object Serialize(object message)
        {
            using (var msi = new MemoryStream((byte[])base.Serialize(message)))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionLevel.Fastest))
                {
                    msi.CopyTo(gs);
                }

                return mso.ToArray();
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
                using (var streamWriter = new StreamWriter(gs))
                {
                    SerializeCore(streamWriter, message);

                    streamWriter.Flush();
                }
            }
        }
    }
}