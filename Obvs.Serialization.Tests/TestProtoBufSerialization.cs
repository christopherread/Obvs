using NUnit.Framework;
using Obvs.Serialization.ProtoBuf;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestProtoBufSerialization
    {
        [Test]
        public void ShouldSerializeToProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Has.Length.EqualTo(25));
        }

        [Test]
        public void ShouldDeserializeFromProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new ProtoBufMessageDeserializer<TestMessage>();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message, Is.EqualTo(deserialize));
        }
    }
}
