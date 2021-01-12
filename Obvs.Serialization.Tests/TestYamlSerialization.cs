using System.Linq;
using System.Reflection;
using FakeItEasy;
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