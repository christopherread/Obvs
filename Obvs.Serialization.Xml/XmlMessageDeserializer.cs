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
            using (StringReader stringReader = new StringReader((string)obj))
            {
                return (TMessage)_xmlSerializer.Deserialize(stringReader);
            }
        }
    }
}