using FakeItEasy;
using NUnit.Framework;
using Obvs.Configuration;
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

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = NetJsonDefaults.Encoding.GetString(serializer.Serialize(message));

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Is.Not.Empty);
            Assert.That(serialize, Contains.Substring(message.Id.ToString()));
            Assert.That(serialize, Contains.Substring(message.Name));
        }

        [Test]
        public void ShouldDeserializeFromNetJson()
        {
            IMessageSerializer serializer = new NetJsonMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new NetJsonMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };

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