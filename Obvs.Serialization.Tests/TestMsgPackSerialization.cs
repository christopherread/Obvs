using System;
using NUnit.Framework;
using Obvs.Serialization.MessagePack;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestMsgPackSerialization
    {
        [Test]
        public void ShouldSerializeToMsgPack()
        {
            IMessageSerializer serializer = new MsgPackMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Has.Length.EqualTo(20));
        }

        [Test]
        public void ShouldDeserializeFromMsgPack()
        {
            IMessageSerializer serializer = new MsgPackMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new MsgPackMessageDeserializer<TestMessage>();

            // see MsgPack spec limitation regarding UTC dates
            // https://github.com/msgpack/msgpack-cli/wiki#datetime
            var message = new TestMessage { Id = 123, Name = "SomeName", Date = new DateTime(2010, 2, 10, 13, 22, 59, DateTimeKind.Utc) };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message.Id, Is.EqualTo(deserialize.Id));
            Assert.That(message.Name, Is.EqualTo(deserialize.Name));
            Assert.That(message.Date, Is.EqualTo(deserialize.Date));
        }
    }
}