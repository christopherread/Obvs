using System.Linq;
using System.Reflection;
using FakeItEasy;
using Xunit;
using Obvs.Serialization.Json;
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
            var des = deses.FirstOrDefault(d => d.TypeName == typeof(TestMessage).Name);

            IMessageSerializer serializer = new JsonMessageSerializer();
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };
            var bytes = serializer.Serialize(messageBefore);

            var messageAfter = des.Deserialize(bytes);

            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldCreateADeserializerForAGivenType()
        {
            // For services that register their messages with AddMessages, so nothing is scanned
            var factory = (IMessageTypeDeserializerFactory)new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>));
            var deserializer = factory.Create<IMessage>(typeof(TestMessage));
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var messageAfter = deserializer.Deserialize(new JsonMessageSerializer().Serialize(messageBefore));

            Assert.Equal(typeof(TestMessage).Name, deserializer.TypeName);
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
        public void ShouldNotShipACopyOfArrayPool()
        {
            // v6 shipped a copy of System.Buffers' ArrayPool<T>; v7 pools Newtonsoft's buffers with .NET's own
            Assert.DoesNotContain(typeof(JsonMessageSerializer).Assembly.GetTypes(), type => type.Name.StartsWith("ArrayPool") || type.Namespace == "Obvs.Serialization.Json.Utils");
        }

        [Fact]
        public void ShouldRoundTripAMessageLargerThanOnePooledBuffer()
        {
            IMessageSerializer serializer = new JsonMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new JsonMessageDeserializer<TestMessageProto>();
            var message = new TestMessageProto { Id = 123, Name = new string('x', 100_000) };

            Assert.Equal(message, deserializer.Deserialize(serializer.Serialize(message)));
        }
    }
}