using FakeItEasy;
using Xunit;
using Obvs.Configuration;
using Obvs.Serialization.ProtoBuf;
using Obvs.Serialization.ProtoBuf.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    
    public class TestProtoBufSerialization
    {
        [Fact]
        public void ShouldSerializeToProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.NotNull(serialize);
            Assert.Equal(serialize.Length, 25);
        }

        [Fact]
        public void ShouldDeserializeFromProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new ProtoBufMessageDeserializer<TestMessage>();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message, deserialize);
        }

        [Fact]
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
