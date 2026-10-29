using FakeItEasy;
using Xunit;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{
    
    public class TestProtoBufSerialization
    {
        [Fact]
        public void ShouldCreateADeserializerForAGivenType()
        {
            // For services that register their messages with AddMessages, so nothing is scanned
            var factory = (IMessageTypeDeserializerFactory)new ProtoBufMessageDeserializerFactory();
            var deserializer = factory.Create<IMessage>(typeof(TestMessageProto));
            var messageBefore = new TestMessageProto { Id = 123, Name = "SomeName" };

            var messageAfter = deserializer.Deserialize(new ProtoBufMessageSerializer().Serialize(messageBefore));

            Assert.Equal(typeof(TestMessageProto).Name, deserializer.GetTypeName());
            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldSerializeToProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.NotNull(serialize);
            Assert.Equal(25, serialize.Length);
        }

        [Fact]
        public void ShouldDeserializeFromProtoBuf()
        {
            IMessageSerializer serializer = new ProtoBufMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new ProtoBufMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message, deserialize);
        }
    }
}
