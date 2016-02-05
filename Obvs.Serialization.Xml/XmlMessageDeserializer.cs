using System.IO;
using System.Xml.Serialization;

namespace Obvs.Serialization.Xml
{
    public class XmlMessageDeserializer<TMessage> : MessageDeserializerBase<TMessage>
        where TMessage : class
    {
        private readonly XmlSerializer _xmlSerializer;

        public XmlMessageDeserializer()
        {
            _xmlSerializer = new XmlSerializer(typeof(TMessage));
        }

        public override TMessage Deserialize(Stream stream)
        {
            using (TextReader reader = new StreamReader(stream, XmlSerializerDefaults.Encoding, true, 1024, true))
            {
                return (TMessage)_xmlSerializer.Deserialize(reader);
            }
        }
    }
}