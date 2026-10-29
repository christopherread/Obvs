using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

using FakeItEasy;

using MessagePack.Resolvers;

using Obvs.Serialization.MessagePack;
using Obvs.Types;

using Xunit;

namespace Obvs.Serialization.Tests {
    
    public class TestMessagePackCSharpSerialization {
        [Fact]
        public void ShouldCreateADeserializerForAGivenType() {
            // For services that register their messages with AddMessages, so nothing is scanned
            var factory = (IMessageTypeDeserializerFactory)new MessagePackCSharpMessageDeserializerFactory();
            var deserializer = factory.Create<IMessage>(typeof(TestMessage));
            var messageBefore = CreateFixedMessage(); // A UTC date, which MessagePack keeps as is

            var messageAfter = deserializer.Deserialize(new MessagePackCSharpMessageSerializer().Serialize(messageBefore));

            Assert.Equal(typeof(TestMessage).Name, deserializer.GetTypeName());
            Assert.Equal(messageBefore, messageAfter);
        }

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

        // Bytes written by MessagePack-CSharp 1.9.11, so services on either version can talk to each other
        private const string Version1Bytes = "94C07BA8536F6D654E616D65D6FF4B72B333";

        private static TestMessage CreateFixedMessage() {
            return new TestMessage { Id = 123, Name = "SomeName", Date = new DateTime(2010, 2, 10, 13, 22, 59, DateTimeKind.Utc) };
        }

        [Fact]
        public void ShouldSerializeToSameBytesAsVersion1() {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer();

            var serialize = serializer.Serialize(CreateFixedMessage());

            Assert.Equal(Version1Bytes, Convert.ToHexString(serialize));
        }

        [Fact]
        public void ShouldDeserializeBytesWrittenByVersion1() {
            IMessageDeserializer<TestMessage> deserializer = new MessagePackCSharpMessageDeserializer<TestMessage>();

            var deserialize = deserializer.Deserialize(Convert.FromHexString(Version1Bytes));

            Assert.Equal(CreateFixedMessage(), deserialize);
        }

        [Fact]
        public void ShouldUseResolverPassedToConstructor() {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer(ContractlessStandardResolver.Instance);
            IMessageDeserializer<UnattributedMessage> deserializer = new MessagePackCSharpMessageDeserializer<UnattributedMessage>(ContractlessStandardResolver.Instance);

            var serialize = serializer.Serialize(new UnattributedMessage { Name = "SomeName" });
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal("SomeName", deserialize.Name);
        }

        [Fact]
        public void ShouldDeserializeModeratelyNestedMessages() {
            IMessageDeserializer<NestedMessage> deserializer = new MessagePackCSharpMessageDeserializer<NestedMessage>();

            var deserialize = deserializer.Deserialize(CreateNestedPayload(10));

            Assert.NotNull(deserialize.Child);
        }

        [Fact]
        public void ShouldRejectExcessivelyNestedMessages() {
            IMessageDeserializer<NestedMessage> deserializer = new MessagePackCSharpMessageDeserializer<NestedMessage>();

            Assert.ThrowsAny<Exception>(() => deserializer.Deserialize(CreateNestedPayload(1000)));
        }

        [Fact]
        public void ShouldDeserializeDictionariesWithHashCollisionResistantKeys() {
            IMessageSerializer serializer = new MessagePackCSharpMessageSerializer();
            IMessageDeserializer<DictionaryMessage> deserializer = new MessagePackCSharpMessageDeserializer<DictionaryMessage>();

            var serialize = serializer.Serialize(new DictionaryMessage { Values = new Dictionary<string, int> { ["a"] = 1 } });
            var deserialize = deserializer.Deserialize(serialize);

            Assert.Equal(1, deserialize.Values["a"]);
            Assert.NotSame(EqualityComparer<string>.Default, deserialize.Values.Comparer);
        }

        // A NestedMessage nested 'depth' times: each level is a one-element array holding its child
        private static byte[] CreateNestedPayload(int depth) {
            return Enumerable.Repeat((byte)0x91, depth).Append((byte)0xC0).ToArray();
        }
    }

    public class UnattributedMessage : IMessage {
        public string Name { get; set; }
    }

    [DataContract]
    public class NestedMessage : IMessage {
        [DataMember(Order = 0)]
        public NestedMessage Child { get; set; }
    }

    [DataContract]
    public class DictionaryMessage : IMessage {
        [DataMember(Order = 0)]
        public Dictionary<string, int> Values { get; set; }
    }
}