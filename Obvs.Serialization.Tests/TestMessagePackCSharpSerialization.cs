using System;
using FakeItEasy;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Serialization.MessagePack;
using Obvs.Serialization.MessagePack.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestMessagePackCSharpSerialization
    {
        [Test]
        public void ShouldSerializeToMessagePackCSharp()
        {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Has.Length.EqualTo(22));
        }

        [Test]
        public void ShouldDeserializeFromMessagePackCSharp()
        {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new MessagePackCSharpMessageDeserializer<TestMessage>();

            var message = new TestMessage { Id = 123, Name = "SomeName", Date = new DateTime(2010, 2, 10, 13, 22, 59, DateTimeKind.Utc) };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message.Id, Is.EqualTo(deserialize.Id));
            Assert.That(message.Name, Is.EqualTo(deserialize.Name));
            Assert.That(message.Date, Is.EqualTo(deserialize.Date));
            Assert.That(message.Date.Kind, Is.EqualTo(deserialize.Date.Kind));
        }

        [Test]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsMessagePackCSharp();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof(MessagePackCSharpMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof(MessagePackCSharpMessageDeserializerFactory))))
                .MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}