using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Obvs.AmazonSqs.Tests.Hosting;
using Obvs.Transports;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AmazonSqs.Tests
{
    // How a message becomes an SQS or SNS body and message attributes, and back
    public class TestMessageCodec
    {
        private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

        [Fact]
        public void ShouldKeepTheTypeNameInItsOwnAttribute()
        {
            var encoded = MessageCodec.Encode(new PlaceOrder { OrderId = "o-1" }, NoHeaders, new JsonMessageSerializer());

            Assert.Equal(nameof(PlaceOrder), encoded.Attributes[MessagePropertyNames.TypeName]);
            Assert.Equal(nameof(PlaceOrder), MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes)).TypeName);
        }

        [Fact]
        public void ShouldPackEveryHeaderIntoOneAttribute()
        {
            // SQS and SNS allow 10 message attributes, so headers can't have one each
            var message = new PlaceOrder { OrderId = "o-1" };
            var headers = Enumerable.Range(0, 20).ToDictionary(i => $"x-header-{i}", i => $"value \"{i}\", with: punctuation");
            headers[ObvsMessageHeaders.TraceParent] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

            var encoded = MessageCodec.Encode(message, headers, new JsonMessageSerializer());
            var decoded = MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes));

            Assert.True(encoded.Attributes.Count <= 10);
            Assert.True(encoded.Attributes.ContainsKey(MessageCodec.HeadersAttribute));
            Assert.Equal(headers.OrderBy(header => header.Key), decoded.Headers.OrderBy(header => header.Key));
        }

        [Fact]
        public void ShouldNotCarryTheTypeNameAsAHeader()
        {
            var headers = new Dictionary<string, string> { [MessagePropertyNames.TypeName] = "Spoofed", ["x-tenant"] = "acme" };

            var encoded = MessageCodec.Encode(new PlaceOrder { OrderId = "o-1" }, headers, new JsonMessageSerializer());
            var decoded = MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes));

            Assert.Equal(nameof(PlaceOrder), decoded.TypeName);
            Assert.Equal(new[] { "x-tenant" }, decoded.Headers.Keys);
        }

        [Fact]
        public void ShouldLeaveOutTheHeadersAttributeWhenThereAreNoHeaders()
        {
            var encoded = MessageCodec.Encode(new PlaceOrder { OrderId = "o-1" }, NoHeaders, new JsonMessageSerializer());

            Assert.False(encoded.Attributes.ContainsKey(MessageCodec.HeadersAttribute));
            Assert.Empty(MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes)).Headers);
        }

        [Fact]
        public void ShouldSendTextBodiesAsTheyAre()
        {
            var encoded = MessageCodec.Encode(new PlaceOrder { OrderId = "o-1" }, NoHeaders, new JsonMessageSerializer());

            Assert.Contains("\"OrderId\":\"o-1\"", encoded.Body);
            Assert.False(encoded.Attributes.ContainsKey(MessageCodec.BodyEncodingAttribute));
            Assert.Equal(new JsonMessageSerializer().Serialize(new PlaceOrder { OrderId = "o-1" }), MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes)).Body);
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x01, 0x02 })] // A control character SQS doesn't allow
        [InlineData(new byte[] { 0xFF, 0xFE, 0x41 })] // Not UTF-8
        [InlineData(new byte[] { 0x41, 0xEF, 0xBF, 0xBF })] // U+FFFF, which SQS doesn't allow
        public void ShouldBase64EncodeBodiesSqsCantCarryAsText(byte[] bytes)
        {
            var encoded = MessageCodec.Encode(new PlaceOrder(), NoHeaders, new BytesSerializer(bytes));

            Assert.Equal(Convert.ToBase64String(bytes), encoded.Body);
            Assert.Equal("base64", encoded.Attributes[MessageCodec.BodyEncodingAttribute]);
            Assert.Equal(bytes, MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes)).Body);
        }

        [Fact]
        public void ShouldCarryTheReplyAddress()
        {
            const string replyTo = "http://localhost:4566/000000000000/orders-responses-worker-1";

            var encoded = MessageCodec.Encode(new GetQuote { Sku = "sku-1" }, NoHeaders, new JsonMessageSerializer(), replyTo);

            Assert.Equal(replyTo, MessageCodec.Decode(encoded.Body, Copy(encoded.Attributes)).ReplyTo);
            Assert.Null(MessageCodec.Decode(encoded.Body, new Dictionary<string, string> { [MessagePropertyNames.TypeName] = nameof(GetQuote) }).ReplyTo);
        }

        [Fact]
        public void ShouldRejectAMessageOverTheSizeLimitNamingTheLimit()
        {
            var tooLarge = Encoding.UTF8.GetBytes(new string('a', MessageCodec.MaxMessageBytes + 1));

            var exception = Assert.Throws<InvalidOperationException>(() => MessageCodec.Encode(new PlaceOrder(), NoHeaders, new BytesSerializer(tooLarge)));

            Assert.Contains(nameof(PlaceOrder), exception.Message);
            Assert.Contains("262,144", exception.Message);
            Assert.Contains("256 KiB", exception.Message);
        }

        [Fact]
        public void ShouldCountAttributesTowardsTheSizeLimit()
        {
            // Only the body's bytes are under the limit; its attributes take it over
            var headers = new Dictionary<string, string> { ["x-padding"] = new string('p', 100) };
            var body = Encoding.UTF8.GetBytes(new string('a', MessageCodec.MaxMessageBytes - 50));

            Assert.Throws<InvalidOperationException>(() => MessageCodec.Encode(new PlaceOrder(), headers, new BytesSerializer(body)));
        }

        [Fact]
        public void ShouldAcceptAMessageAtTheSizeLimit()
        {
            var probe = MessageCodec.Encode(new PlaceOrder(), NoHeaders, new BytesSerializer(Array.Empty<byte>()));
            var attributeBytes = probe.Attributes.Sum(attribute => Encoding.UTF8.GetByteCount(attribute.Key) + "String".Length + Encoding.UTF8.GetByteCount(attribute.Value));
            var body = Encoding.UTF8.GetBytes(new string('a', MessageCodec.MaxMessageBytes - attributeBytes));

            var encoded = MessageCodec.Encode(new PlaceOrder(), NoHeaders, new BytesSerializer(body));

            Assert.Equal(body.Length, encoded.Body.Length);
        }

        private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> attributes)
        {
            return attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        }

        private sealed class BytesSerializer : IMessageSerializer
        {
            private readonly byte[] _bytes;

            public BytesSerializer(byte[] bytes) => _bytes = bytes;

            public void Serialize(Stream destination, object message) => destination.Write(_bytes, 0, _bytes.Length);
        }
    }
}
