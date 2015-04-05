using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Obvs.Serialization.Xml
{
    public class XmlMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
    {
        private readonly XmlSerializer _xmlSerializer;

        public XmlMessageDeserializer()
        {
            _xmlSerializer = new XmlSerializer(typeof(TMessage));
        }

        public override TMessage Deserialize(object obj)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes((string)obj)))
            {
                return Deserialize(stream);
            }
        }

        public override TMessage Deserialize(Stream stream)
        {
            return (TMessage)_xmlSerializer.Deserialize(stream);
        }
    }
}