using System.Text;
using NUnit.Framework;
using Obvs.Serialization.Xml;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestXmlSerialization
    {
        [Test]
        public void ShouldSerializeToXml()
        {
            IMessageSerializer serializer = new XmlMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message) as string;

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Contains.Substring(message.Id.ToString()));
            Assert.That(serialize, Contains.Substring(message.Name));
        }

        [Test]
        public void ShouldDeserializeFromXml()
        {
            IMessageSerializer serializer = new XmlMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new XmlMessageDeserializer<TestMessage>();

            var message = new TestMessage {Id = 123, Name = "SomeName"};
            var serialize = serializer.Serialize(message) as string;
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message, Is.EqualTo(deserialize));
        }
        
        [Test]
        public void ShouldDeserializeFromXmlAscii()
        {
            IMessageSerializer serializer = new XmlMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new XmlMessageDeserializer<TestMessage>();

            var message = new TestMessage {Id = 123, Name = "SomeName"};
            var serialize = (string)serializer.Serialize(message);
            string ascii = Encoding.ASCII.GetString(Encoding.Convert(Encoding.UTF8, Encoding.ASCII, Encoding.UTF8.GetBytes(serialize)));
            var deserialize = deserializer.Deserialize(ascii);

            Assert.That(message, Is.EqualTo(deserialize));
        }
    }
}