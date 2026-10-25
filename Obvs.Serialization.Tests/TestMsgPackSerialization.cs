// These tests exercise the deprecated MsgPack.Cli serializer on purpose
#pragma warning disable CS0618

using System;
using System.Reflection;
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
        [Theory]
        [InlineData(typeof(MsgPackMessageSerializer))]
        [InlineData(typeof(MsgPackMessageDeserializer<>))]
        [InlineData(typeof(MsgPackMessageDeserializerFactory))]
        public void ShouldBeObsoleteInFavourOfMessagePackCSharp(Type type)
        {
            var obsolete = type.GetCustomAttribute<ObsoleteAttribute>();

            Assert.NotNull(obsolete);
            Assert.False(obsolete.IsError);
            Assert.Contains("Obvs.Serialization.MessagePack-CSharp", obsolete.Message);
        }

        [Fact]
        public void FluentConfigShouldBeObsoleteInFavourOfMessagePackCSharp()
        {
            // Obvs.Serialization.MessagePack-CSharp has a class with the same full name, so look it up in this package's assembly
            var extensions = typeof(MsgPackMessageSerializer).Assembly.GetType("Obvs.Serialization.MessagePack.Configuration.FluentConfigExtensions");
            var obsolete = extensions.GetMethod("SerializedAsMsgPack").GetCustomAttribute<ObsoleteAttribute>();

            Assert.NotNull(obsolete);
            Assert.Contains("SerializedAsMessagePackCSharp", obsolete.Message);
        }

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