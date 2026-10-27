using System;
using System.IO;
using System.IO.Compression;
using Obvs.Serialization.Compression;
using Obvs.Serialization.Json;
using Obvs.Serialization.MessagePack;
using Obvs.Serialization.ProtoBuf;
using Obvs.Serialization.SystemTextJson;
using Obvs.Types;
using Xunit;

namespace Obvs.Serialization.Tests
{
    // Compressed(): gzip around whatever a service's serializer writes. Based on #36 by @fernando-almeida.
    public class TestCompression
    {
        private const long Limit = 16 * 1024 * 1024;

        public static TheoryData<string> Serializers => new TheoryData<string> { "SystemTextJson", "Json", "MessagePack", "ProtoBuf" };

        [Theory]
        [MemberData(nameof(Serializers))]
        public void ShouldRoundTripWithAnySerializer(string name)
        {
            var (serializer, factory, message) = SerializerFor(name);
            var compressed = new GzipMessageSerializer(serializer, CompressionLevel.Optimal);
            var decompressing = new GzipMessageDeserializerFactory(factory, Limit, "orders");

            var bytes = compressed.Serialize(message);
            var deserializer = decompressing.Create<IMessage>(message.GetType());

            Assert.Equal(message, deserializer.Deserialize(bytes));
            Assert.Equal(message.GetType().Name, deserializer.GetTypeName());
        }

        [Theory]
        [MemberData(nameof(Serializers))]
        public void ShouldCompress(string name)
        {
            var (serializer, _, _) = SerializerFor(name);
            var message = new TestMessage { Id = 1, Name = new string('x', 10000), Date = FixedDate };

            var plain = serializer.Serialize(message);
            var compressed = new GzipMessageSerializer(serializer, CompressionLevel.Fastest).Serialize(message);

            Assert.True(compressed.Length < plain.Length / 10, $"{compressed.Length} bytes compressed, from {plain.Length}");
            Assert.Equal(new byte[] { 0x1f, 0x8b }, new[] { compressed[0], compressed[1] }); // gzip's magic number
        }

        [Fact]
        public void ShouldCreateDeserializersForAServicesMessagesByScanning()
        {
            var decompressing = new GzipMessageDeserializerFactory(new SystemTextJsonMessageDeserializerFactory(), Limit, "orders");
            var message = new TestMessage { Id = 1, Name = "a", Date = FixedDate };

            var deserializers = decompressing.Create<TestMessage, IMessage>(assembly => !assembly.FullName.Contains("xunit"));

            var deserializer = Assert.Single(deserializers, d => d.GetTypeName() == nameof(TestMessage));
            Assert.Equal(message, deserializer.Deserialize(new GzipMessageSerializer(new SystemTextJsonMessageSerializer(), CompressionLevel.Fastest).Serialize(message)));
        }

        [Fact]
        public void ShouldRefuseAMessageThatDecompressesPastTheLimit()
        {
            var message = new TestMessage { Id = 1, Name = new string('x', 100000), Date = FixedDate };
            var bytes = new GzipMessageSerializer(new SystemTextJsonMessageSerializer(), CompressionLevel.Optimal).Serialize(message);
            var deserializer = new GzipMessageDeserializerFactory(new SystemTextJsonMessageDeserializerFactory(), 1000, "orders").Create<IMessage>(typeof(TestMessage));

            var exception = Assert.Throws<InvalidDataException>(() => deserializer.Deserialize(bytes));

            Assert.Contains("'orders'", exception.Message);
            Assert.Contains(nameof(TestMessage), exception.Message);
            Assert.Contains("1000 bytes", exception.Message);
            Assert.Contains("maxDecompressedBytes", exception.Message);
            Assert.Contains("Compressed(", exception.Message);
        }

        [Fact]
        public void ShouldReadAMessageAtTheLimit()
        {
            var message = new TestMessage { Id = 1, Name = "a", Date = FixedDate };
            var plainLength = new SystemTextJsonMessageSerializer().Serialize(message).Length;
            var bytes = new GzipMessageSerializer(new SystemTextJsonMessageSerializer(), CompressionLevel.Fastest).Serialize(message);
            var deserializer = new GzipMessageDeserializerFactory(new SystemTextJsonMessageDeserializerFactory(), plainLength, "orders").Create<IMessage>(typeof(TestMessage));

            Assert.Equal(message, deserializer.Deserialize(bytes));
        }

        // v6 services compress JSON with Obvs.Serialization.Json's GzippedJson serializers
#pragma warning disable CS0618 // They're obsolete, and kept for this
        [Fact]
        public void ShouldWriteTheSameBytesAsV6sGzippedJson()
        {
            var message = new TestMessage { Id = 123, Name = "SomeName", Date = FixedDate };

            var v6 = new GzippedJsonMessageSerializer(null).Serialize(message);
            var v7 = new GzipMessageSerializer(new JsonMessageSerializer(), CompressionLevel.Fastest).Serialize(message);

            Assert.Equal(v6, v7);
        }

        [Fact]
        public void ShouldReadV6sGzippedJson()
        {
            var message = new TestMessage { Id = 123, Name = "SomeName", Date = FixedDate };
            var v6 = new GzippedJsonMessageSerializer(null).Serialize(message);
            var deserializer = new GzipMessageDeserializerFactory(new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)), Limit, "orders")
                .Create<IMessage>(typeof(TestMessage));

            Assert.Equal(message, deserializer.Deserialize(v6));
        }

        [Fact]
        public void ShouldWriteWhatV6sGzippedJsonReads()
        {
            var message = new TestMessage { Id = 123, Name = "SomeName", Date = FixedDate };
            var v7 = new GzipMessageSerializer(new JsonMessageSerializer(), CompressionLevel.Optimal).Serialize(message);

            Assert.Equal(message, new GzippedJsonMessageDeserializer<TestMessage>(null).Deserialize(v7));
        }
#pragma warning restore CS0618

        // A UTC date, which every serializer keeps as is
        private static readonly DateTime FixedDate = new DateTime(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc);

        private static (IMessageSerializer Serializer, IMessageDeserializerFactory Factory, IMessage Message) SerializerFor(string name)
        {
            switch (name)
            {
                case "SystemTextJson":
                    return (new SystemTextJsonMessageSerializer(), new SystemTextJsonMessageDeserializerFactory(), new TestMessage { Id = 1, Name = "a", Date = FixedDate });
                case "Json":
                    return (new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)), new TestMessage { Id = 1, Name = "a", Date = FixedDate });
                case "MessagePack":
                    return (new MessagePackCSharpMessageSerializer(), new MessagePackCSharpMessageDeserializerFactory(), new TestMessage { Id = 1, Name = "a", Date = FixedDate });
                default:
                    return (new ProtoBufMessageSerializer(), new ProtoBufMessageDeserializerFactory(), new TestMessageProto { Id = 1, Name = "a", Date = FixedDate });
            }
        }
    }
}
