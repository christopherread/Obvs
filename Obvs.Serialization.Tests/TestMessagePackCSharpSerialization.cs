using System;

using FakeItEasy;

using Obvs.Configuration;
using Obvs.Serialization.MessagePack;
using Obvs.Serialization.MessagePack.Configuration;
using Obvs.Types;

using Xunit;

namespace Obvs.Serialization.Tests {
    
    public class TestMessagePackCSharpSerialization {
        [Fact]
        public void ShouldSerializeToMessagePackCSharp() {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer();

            var message = new TestMessage { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);

            Assert.NotNull(serialize);
            Assert.NotEmpty(serialize);
            Assert.Equal(22, serialize.Length);
        }

        [Fact]
        public void ShouldDeserializeFromMessagePackCSharp() {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer();
            IMessageDeserializer<TestMessage> deserializer = new MessagePackCSharpMessageDeserializer<TestMessage>();

            var message = new TestMessage { Id = 123, Name = "SomeName", Date = new DateTime(2010, 2, 10, 13, 22, 59, DateTimeKind.Utc) };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message.Id, deserialize.Id);
            Assert.Equal(message.Name, deserialize.Name);
            Assert.Equal(message.Date, deserialize.Date);
            Assert.Equal(message.Date.Kind, deserialize.Date.Kind);
        }

        [Fact]
        public void ShouldPassInCorrectFluentConfig() {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsMessagePackCSharp();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                    A<IMessageSerializer>.That.IsInstanceOf(typeof(MessagePackCSharpMessageSerializer)),
                    A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof(MessagePackCSharpMessageDeserializerFactory))))
                .MustHaveHappened(1, Times.Exactly);
        }
    }
}