using System.IO;
using System.Xml.Serialization;

namespace Obvs.Serialization
{
    public class XmlMessageDeserializer<TMessage> : DeserializerBase<TMessage>, IMessageDeserializer<TMessage>
    {
        private readonly XmlSerializer _xmlSerializer;

        public XmlMessageDeserializer()
        {
            _xmlSerializer = new XmlSerializer(typeof(TMessage));
        }

        public TMessage Deserialize(object obj)
        {
            using (StringReader stringReader = new StringReader((string)obj))
            {
                return (TMessage)_xmlSerializer.Deserialize(stringReader);
            }
        }
    }
}