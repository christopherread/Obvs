using System;
using System.Collections.Concurrent;
using System.IO;
using System.Xml.Serialization;

namespace Obvs.Serialization.Xml
{
    public class XmlMessageSerializer : IMessageSerializer
    {
        private readonly ConcurrentDictionary<Type, XmlSerializer> _serializers = new ConcurrentDictionary<Type, XmlSerializer>();

        public object Serialize(object obj)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Serialize(stream, obj);
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public void Serialize(Stream stream, object obj)
        {
            Serializer(obj.GetType()).Serialize(stream, obj);
        }

        private XmlSerializer Serializer(Type type)
        {
            return _serializers.GetOrAdd(type, t => new XmlSerializer(t));
        }
    }
}