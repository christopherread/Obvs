using System.Linq;
using FakeItEasy;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Serialization.Json;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestJsonSerialization
    {
        [Test]
        public void JsonDeserializerFactoryShouldWork()
        {
            var factory = new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>));
            var deses = factory.Create<TestMessageProto, IMessage>();

            var des = deses.FirstOrDefault(d => d.GetTypeName() == typeof(TestMessageProto).Name);

            IMessageSerializer serializer = new JsonMessageSerializer();
            var messageBefore = new TestMessageProto { Id = 123, Name = "SomeName" };
            var bytes = serializer.Serialize(messageBefore);

            var messageAfter = des.Deserialize(bytes);

            Assert.AreEqual(messageBefore, messageAfter);
        }

        [Test]
        public void ShouldSerializeToJson()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = JsonMessageDefaults.Encoding.GetString(serializer.Serialize(message));

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Contains.Substring(message.Id.ToString()));
            Assert.That(serialize, Contains.Substring(message.Name));
        }

        [Test]
        public void ShouldDeserializeFromJson()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new JsonMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message, Is.EqualTo(deserialize));
        }

        [Test]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsJson();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof (JsonMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof (JsonMessageDeserializerFactory))))
                .MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}