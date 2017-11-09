using FakeItEasy;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Serialization.ProtoBuf;
using Obvs.Serialization.ProtoBuf.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    [TestFixture]
    public class TestProtoBufSerialization
    {
        [Test]
        public void ShouldSerializeToProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize, Has.Length.EqualTo(25));
        }

        [Test]
        public void ShouldDeserializeFromProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new ProtoBufMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.That(message, Is.EqualTo(deserialize));
        }

        [Test]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsProtoBuf();
            
            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof (ProtoBufMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof (ProtoBufMessageDeserializerFactory))))
                .MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}
