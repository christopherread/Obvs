using System;
using System.Linq;
using System.Reflection;
using FakeItEasy;
using YamlDotNet.Serialization;
using Xunit;
using Obvs.Configuration;
using Obvs.Serialization.Yaml;
using Obvs.Serialization.Yaml.Configuration;
using Obvs.Types;

namespace Obvs.Serialization.Tests
{

    public class TestYamlSerialization
    {
        [Fact]
        public void YamlDeserializerFactoryShouldWork()
        {
            var factory = new YamlMessageDeserializerFactory(typeof(YamlMessageDeserializer<>));
            var deses = factory.Create<TestMessage, IMessage>(ShouldLoadAssembly);
            var des = deses.FirstOrDefault(d => d.GetTypeName() == typeof(TestMessage).Name);

            IMessageSerializer serializer = new YamlMessageSerializer();
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
        public void ShouldSerializeToYaml()
        {
            IMessageSerializer serializer = new YamlMessageSerializer();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = YamlMessageDefaults.Encoding.GetString(serializer.Serialize(message));

            Assert.NotNull(serialize);
            Assert.Contains(message.Id.ToString(), serialize);
            Assert.Contains(message.Name, serialize);
        }

        [Fact]
        public void ShouldDeserializeFromYaml()
        {
            IMessageSerializer serializer = new YamlMessageSerializer();
            IMessageDeserializer<TestMessageProto> deserializer = new YamlMessageDeserializer<TestMessageProto>();

            var message = new TestMessageProto { Id = 123, Name = "SomeName" };
            var serialize = serializer.Serialize(message);
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(message, deserialize);
        }

        // Text written by YamlDotNet 6.0.0, so services on either version can talk to each other
        private const string Version6Yaml = "id: 123\nname: SomeName\ndate: 2010-02-10T13:22:59.0000000Z\n";

        private static TestMessage CreateFixedMessage()
        {
            return new TestMessage { Id = 123, Name = "SomeName", Date = new DateTime(2010, 2, 10, 13, 22, 59, DateTimeKind.Utc) };
        }

        [Fact]
        public void ShouldSerializeToSameYamlAsVersion6()
        {
            IMessageSerializer serializer = new YamlMessageSerializer();

            var serialize = YamlMessageDefaults.Encoding.GetString(serializer.Serialize(CreateFixedMessage()));

            Assert.Equal(Version6Yaml, serialize.Replace("\r\n", "\n"));
        }

        [Fact]
        public void ShouldDeserializeYamlWrittenByVersion6()
        {
            IMessageDeserializer<TestMessage> deserializer = new YamlMessageDeserializer<TestMessage>();

            var deserialize = deserializer.Deserialize(YamlMessageDefaults.Encoding.GetBytes(Version6Yaml));

            Assert.Equal(CreateFixedMessage(), deserialize);
            Assert.Equal(DateTimeKind.Utc, deserialize.Date.Kind);
        }

        [Fact]
        public void ShouldUseSerializerAndDeserializerPassedToConstructor()
        {
            IMessageSerializer serializer = new YamlMessageSerializer(new SerializerBuilder().Build());
            IMessageDeserializer<TestMessage> deserializer = new YamlMessageDeserializer<TestMessage>(new DeserializerBuilder().Build());

            var message = CreateFixedMessage();
            var serialize = serializer.Serialize(message);

            var deserialize = deserializer.Deserialize(serialize);

            Assert.Contains("Name: SomeName", YamlMessageDefaults.Encoding.GetString(serialize));
            Assert.Equal(message.Id, deserialize.Id);
            Assert.Equal(message.Name, deserialize.Name);
        }

        [Fact]
        public void ShouldPassInCorrectFluentConfig()
        {
            var fakeConfigurator = A.Fake<ICanSpecifyEndpointSerializers<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            fakeConfigurator.SerializedAsYaml();

            A.CallTo(() => fakeConfigurator.SerializedWith(
                A<IMessageSerializer>.That.IsInstanceOf(typeof(YamlMessageSerializer)),
                A<IMessageDeserializerFactory>.That.IsInstanceOf(typeof(YamlMessageDeserializerFactory))))
                .MustHaveHappened(1, Times.Exactly);
        }
    }
}