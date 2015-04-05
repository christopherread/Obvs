using NUnit.Framework;
using Obvs.Serialization.Json;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestJsonSerialization
    {
        [Test]
        public void ShouldSerializeToJson()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Contains.Substring(message.Id.ToString()));
            Assert.That(serialize, Contains.Substring(message.Name));
        }

        [Test]
        public void ShouldDeserializeFromJson()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new JsonMessageDeserializer<TestMessage>();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message, Is.EqualTo(deserialize));
        }
    }
}