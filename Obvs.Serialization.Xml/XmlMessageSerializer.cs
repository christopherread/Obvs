using System.IO;
using System.Xml.Serialization;

namespace Obvs.Serialization.Xml
{
    public class XmlMessageSerializer<TMessage> : IMessageSerializer
    {
        private readonly XmlSerializer _xmlSerializer;

        public XmlMessageSerializer()
        {
            _xmlSerializer = new XmlSerializer(typeof(TMessage));
        }

        public object Serialize(object obj)
        {
            using (StringWriter writer = new StringWriter())
            {
                _xmlSerializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }
    }
}