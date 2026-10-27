using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Obvs.Serialization.Json;
using Obvs.Serialization.SystemTextJson;
using Obvs.Types;
using Xunit;

namespace Obvs.Serialization.Tests
{
    public class TestSystemTextJsonSerialization
    {
        [Fact]
        public void ShouldRoundTripAMessage()
        {
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var bytes = new SystemTextJsonMessageSerializer().Serialize(messageBefore);
            var messageAfter = new SystemTextJsonMessageDeserializer<TestMessage>().Deserialize(bytes);

            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldSerializeTheMessagesRuntimeType()
        {
            IMessage message = new TestMessage { Id = 123, Name = "SomeName" };

            var json = Encoding.UTF8.GetString(new SystemTextJsonMessageSerializer().Serialize(message));

            Assert.Contains("\"Id\":123", json);
            Assert.Contains("\"Name\":\"SomeName\"", json);
        }

        [Fact]
        public void ShouldCreateDeserializersForAServicesMessages()
        {
            var factory = new SystemTextJsonMessageDeserializerFactory();
            var deserializer = factory.Create<TestMessage, IMessage>(ShouldLoadAssembly)
                .Single(d => d.GetTypeName() == typeof(TestMessage).Name);
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var messageAfter = deserializer.Deserialize(new SystemTextJsonMessageSerializer().Serialize(messageBefore));

            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldCreateADeserializerForAGivenType()
        {
            var factory = (IMessageTypeDeserializerFactory)new SystemTextJsonMessageDeserializerFactory();
            var deserializer = factory.Create<IMessage>(typeof(TestMessage));
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var messageAfter = deserializer.Deserialize(new SystemTextJsonMessageSerializer().Serialize(messageBefore));

            Assert.Equal(typeof(TestMessage).Name, deserializer.GetTypeName());
            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldUseTheOptionsGiven()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var bytes = new SystemTextJsonMessageSerializer(options).Serialize(messageBefore);
            var messageAfter = new SystemTextJsonMessageDeserializerFactory(options).Create<IMessage>(typeof(TestMessage)).Deserialize(bytes);

            Assert.Contains("\"name\":\"SomeName\"", Encoding.UTF8.GetString(bytes));
            Assert.Equal(messageBefore, messageAfter);
        }

        // v6 services serialize with Newtonsoft.Json, so v7 services must read what they send, and the other way round
        [Fact]
        public void ShouldReadMessagesSerializedWithNewtonsoftJson()
        {
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var messageAfter = new SystemTextJsonMessageDeserializer<TestMessage>().Deserialize(new JsonMessageSerializer().Serialize(messageBefore));

            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldWriteMessagesNewtonsoftJsonCanRead()
        {
            var messageBefore = new TestMessage { Id = 123, Name = "SomeName" };

            var messageAfter = new JsonMessageDeserializer<TestMessage>().Deserialize(new SystemTextJsonMessageSerializer().Serialize(messageBefore));

            Assert.Equal(messageBefore, messageAfter);
        }

        [Fact]
        public void ShouldReadPropertyNamesInAnyCaseAndPublicFieldsAsNewtonsoftJsonDoes()
        {
            var json = Encoding.UTF8.GetBytes("{\"id\":123,\"NAME\":\"SomeName\",\"Field\":\"f\"}");

            var message = new SystemTextJsonMessageDeserializer<MessageWithField>().Deserialize(json);

            Assert.Equal(123, message.Id);
            Assert.Equal("SomeName", message.Name);
            Assert.Equal("f", message.Field);
        }

        public class MessageWithField : IMessage
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public string Field;
        }

        private static bool ShouldLoadAssembly(Assembly assembly) => !assembly.FullName.ToLowerInvariant().Contains("xunit");
    }
}
