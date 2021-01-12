using System;
using System.Collections.Concurrent;
using System.IO;
using System.Xml.Serialization;

namespace Obvs.Serialization.Xml
{
    public class XmlMessageSerializer : IMessageSerializer
    {
        private readonly ConcurrentDictionary<Type, XmlSerializer> _serializers = new ConcurrentDictionary<Type, XmlSerializer>();

        public void Serialize(Stream stream, object obj)
        {
            using (TextWriter writer = new StreamWriter(stream, XmlSerializerDefaults.Encoding, 1024, true))
            {
                Serializer(obj.GetType()).Serialize(writer, obj);
            }
        }

        private XmlSerializer Serializer(Type type)
        {
            return _serializers.GetOrAdd(type, t => new XmlSerializer(t));
        }
    }
}