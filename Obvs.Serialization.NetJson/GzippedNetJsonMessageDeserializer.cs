using System.IO;
using System.IO.Compression;
using System.Text;
using NetJSON;

namespace Obvs.Serialization.NetJson
{
    public class GzippedNetJsonMessageDeserializer<TMessage> : NetJsonMessageDeserializer<TMessage>
        where TMessage : class
    {
        public override TMessage Deserialize(object obj)
        {
            //using (var msi = new MemoryStream((byte[])obj))
            //using (var mso = new MemoryStream())
            //{
            //    using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            //    {
            //        gs.CopyTo(mso);
            //    }

            //    return base.Deserialize(mso.ToArray());
            //}

            using (var ms = new MemoryStream((byte[])obj))
            {
                return Deserialize(ms);
            }
        }

        public override TMessage Deserialize(Stream stream)
        {
            using (var gs = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                return DeserializeCore(gs);
            }
        }
    }
}