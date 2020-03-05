using System;
using FakeItEasy;
using Xunit;
using Obvs.Configuration;
using Obvs.Serialization.MessagePack;
using Obvs.Serialization.MessagePack.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{

    public class TestMsgPackSerialization
    {
        [Fact]
        public void ShouldSerializeToMsgPack()
        {
            IMessageSerializer serializer = new MsgPackMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);


            Assert.NotNull(serialize);
            Assert.Equal(21, serialize.Length);
        }

        [Fact]
        public void ShouldDeserializeFromMsgPack()
        {
            IMessageSerializer serializer = new MsgPackMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new MsgPackMessageDeserializer<TestMessageProto>();

            // see MsgPack spec limitation regarding UTC dates
            // https://github.com/msgpack/msgpack-cli/wiki#datetime
            var message = new TestMessageProto { Id = 123, Name = "SomeName", Date = new DateTime(2010, 2, 10, 13, 22, 59, DateTimeKind.Utc) };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message.Id, deserialize.Id);
            Assert.Equal(message.Name, deserialize.Name);
            Assert.Equal(message.Date, deserialize.Date);
        }

        [Fact]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsMsgPack();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof(MsgPackMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof(MsgPackMessageDeserializerFactory))))
                .MustHaveHappened(1, Times.Exactly);
        }
    }
}