using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Obvs.Transports;
using Obvs.Serialization;

namespace Obvs.AmazonSqs
{
    // Turns a message into an SQS or SNS body and string message attributes, and back. SQS and SNS allow 10 message
    // attributes, so the type name has one, all Obvs headers (such as trace context) share another, and the rest are
    // used only when needed.
    internal static class MessageCodec
    {
        // The most SQS and SNS take in one message, counting its body and its attributes' names, types and values
        public const int MaxMessageBytes = 256 * 1024;

        public const string HeadersAttribute = "obvs-headers";
        public const string ReplyToAttribute = "obvs-reply-to";
        public const string BodyEncodingAttribute = "obvs-body-encoding";
        public const string StringDataType = "String";

        private const string Base64 = "base64";

        // Throws on bytes that aren't UTF-8, rather than replacing them
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <param name="headers">The Obvs headers (such as trace context) to send with it</param>
        /// <param name="replyTo">The URL of the queue responses should go to, for requests</param>
        public static EncodedMessage Encode(object message, IReadOnlyDictionary<string, string> headers, IMessageSerializer serializer, string replyTo = null)
        {
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, message);
                bytes = stream.ToArray();
            }

            var attributes = new Dictionary<string, string> { [MessagePropertyNames.TypeName] = message.GetType().Name };
            var sent = headers.Where(header => header.Key != MessagePropertyNames.TypeName).ToDictionary(header => header.Key, header => header.Value);
            if (sent.Count > 0)
            {
                attributes[HeadersAttribute] = JsonSerializer.Serialize(sent);
            }
            if (replyTo != null)
            {
                attributes[ReplyToAttribute] = replyTo;
            }

            // Bodies are text: one SQS can't carry as text, such as from a binary serializer, goes as base64
            var body = TryGetText(bytes);
            if (body == null)
            {
                body = Convert.ToBase64String(bytes);
                attributes[BodyEncodingAttribute] = Base64;
            }

            var size = Encoding.UTF8.GetByteCount(body) + attributes.Sum(attribute =>
                Encoding.UTF8.GetByteCount(attribute.Key) + StringDataType.Length + Encoding.UTF8.GetByteCount(attribute.Value));
            if (size > MaxMessageBytes)
            {
                throw new InvalidOperationException(
                    $"{message.GetType().Name} is {size.ToString("N0", CultureInfo.InvariantCulture)} bytes with its message attributes, over the " +
                    $"{MaxMessageBytes.ToString("N0", CultureInfo.InvariantCulture)}-byte (256 KiB) limit Amazon SQS and SNS set on a message. " +
                    "Send less in each message, for example a reference to a larger payload kept elsewhere.");
            }

            return new EncodedMessage(body, attributes);
        }

        public static DecodedMessage Decode(string body, IDictionary<string, string> attributes)
        {
            attributes.TryGetValue(MessagePropertyNames.TypeName, out var typeName);
            attributes.TryGetValue(ReplyToAttribute, out var replyTo);
            var headers = attributes.TryGetValue(HeadersAttribute, out var packed)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(packed)
                : new Dictionary<string, string>();
            var bytes = attributes.TryGetValue(BodyEncodingAttribute, out var encoding) && encoding == Base64
                ? Convert.FromBase64String(body)
                : Encoding.UTF8.GetBytes(body);
            return new DecodedMessage(typeName, bytes, headers, replyTo);
        }

        // The bytes as text, or null if they aren't UTF-8 or hold characters SQS doesn't allow in a message
        private static string TryGetText(byte[] bytes)
        {
            string text;
            try
            {
                text = StrictUtf8.GetString(bytes);
            }
            catch (ArgumentException)
            {
                return null;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var allowed = c == '\t' || c == '\n' || c == '\r' ||
                              (c >= 0x20 && c <= 0xFFFD) ||
                              char.IsSurrogate(c); // Strict decoding only yields surrogates in pairs, for U+10000 and above
                if (!allowed)
                {
                    return null;
                }
            }
            return text;
        }
    }

    internal sealed class EncodedMessage
    {
        public EncodedMessage(string body, IReadOnlyDictionary<string, string> attributes)
        {
            Body = body;
            Attributes = attributes;
        }

        public string Body { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }
    }

    internal sealed class DecodedMessage
    {
        public DecodedMessage(string typeName, byte[] body, IReadOnlyDictionary<string, string> headers, string replyTo)
        {
            TypeName = typeName;
            Body = body;
            Headers = headers;
            ReplyTo = replyTo;
        }

        public string TypeName { get; }
        public byte[] Body { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }
        public string ReplyTo { get; }
    }
}
