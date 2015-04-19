using System.IO;
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
            using (TextReader reader = new StringReader((string) obj))
            {
                return (TMessage)_xmlSerializer.Deserialize(reader);
            }
        }

        public override TMessage Deserialize(Stream stream)
        {
            return (TMessage)_xmlSerializer.Deserialize(stream);
        }
    }
}