using FakeItEasy;
using Xunit;
using Obvs.Configuration;
using Obvs.Serialization.NetJson;
using Obvs.Serialization.NetJson.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    
    public class TestNetJsonSerialization
    {
        [Fact]
        public void ShouldSerializeToNetJson()
        {
            IMessageSerializer serializer = new NetJsonMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = NetJsonDefaults.Encoding.GetString(serializer.Serialize(message));


            Assert.NotNull(serialize);
            Assert.Contains(message.Id.ToString(), serialize);
            Assert.Contains(message.Name, serialize);
        }

        [Fact]
        public void ShouldDeserializeFromNetJson()
        {
            IMessageSerializer serializer = new NetJsonMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new NetJsonMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };

            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message, deserialize);
        }

        [Fact]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsNetJson();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof (NetJsonMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof (NetJsonMessageDeserializerFactory))))
                .MustHaveHappened(1, Times.Exactly);
        }
    }
}