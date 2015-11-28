using System;
using FakeItEasy;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Serialization.Json;
using Obvs.Serialization.Json.Configuration;
using Obvs.Serialization.NetJson;
using Obvs.Serialization.NetJson.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestNetJsonSerialization
    {
        [Test]
        public void ShouldSerializeToNetJson()
        {
            IMessageSerializer serializer = new NetJsonMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Contains.Substring(message.Id.ToString()));
            Assert.That(serialize, Contains.Substring(message.Name));
        }

        [Test]
        public void ShouldDeserializeFromNetJson()
        {
            IMessageSerializer serializer = new NetJsonMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new NetJsonMessageDeserializer<TestMessage>();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            // Truncate to nearest Ms
            message.Date = message.Date.AddTicks(-(message.Date.Ticks % TimeSpan.FromMilliseconds(1).Ticks));

            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message, Is.EqualTo(deserialize));
        }

        [Test]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsNetJson();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof (NetJsonMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof (NetJsonMessageDeserializerFactory))))
                .MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}