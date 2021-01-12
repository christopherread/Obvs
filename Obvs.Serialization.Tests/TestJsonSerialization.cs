using System.Linq;
using System.Reflection;
using FakeItEasy;
using Xunit;
using Obvs.Configuration;
using Obvs.Serialization.Json;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{

    public class TestJsonSerialization
    {
        [Fact]
        public void JsonDeserializerFactoryShouldWork()
        {
            var factory = new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>));
            var deses = factory.Create<TestMessage, IMessage>(ShouldLoadAssembly);
            var des = deses.FirstOrDefault(d => d.GetTypeName() == typeof(TestMessage).Name);

            IMessageSerializer serializer = new JsonMessageSerializer();
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };
            var bytes = serializer.Serialize(messageBefore);

            var messageAfter = des.Deserialize(bytes);

            Assert.Equal(messageBefore, messageAfter);
        }

        private bool ShouldLoadAssembly(Assembly arg)
        {
            if (arg.FullName.ToLowerInvariant().Contains("xunit"))
                return false;

            return true;
        }

        [Fact]
        public void ShouldSerializeToJson()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = JsonMessageDefaults.Encoding.GetString(serializer.Serialize(message));

            Assert.NotNull(serialize);
            Assert.Contains(message.Id.ToString(), serialize);
            Assert.Contains(message.Name, serialize);
        }

        [Fact]
        public void ShouldDeserializeFromJson()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new JsonMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message, deserialize);
        }

        [Fact]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsJson();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof(JsonMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof(JsonMessageDeserializerFactory))))
                .MustHaveHappened(1, Times.Exactly);
        }
    }
}