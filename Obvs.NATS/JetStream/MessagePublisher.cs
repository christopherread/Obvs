using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Obvs.Serialization;
using Obvs.Transports;

namespace Obvs.NATS.JetStream
{
    internal sealed class MessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        // The subject prefix a request's responses go to. JetStream keeps a message's reply subject for its acknowledgement.
        public const string ReplyToHeader = "obvs-reply-to";

        private readonly INatsJSContext _jetStream;
        private readonly string _subjectPrefix;
        private readonly IMessageSerializer _serializer;
        private readonly string _replyTo;

        /// <param name="replyTo">The subject prefix responses should go to, for requests</param>
        public MessagePublisher(INatsJSContext jetStream, string subjectPrefix, IMessageSerializer serializer, string replyTo = null)
        {
            _jetStream = jetStream;
            _subjectPrefix = subjectPrefix;
            _serializer = serializer;
            _replyTo = replyTo;
        }

        public Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return PublishAsync(_jetStream, _subjectPrefix, message, headers, _serializer, _replyTo, cancellationToken);
        }

        // The subject ends in the type name, which tells the receiver which deserializer to use. Obvs headers (such as
        // trace context) become NATS headers. The stream acknowledges the message once it's stored.
        public static async Task PublishAsync(INatsJSContext jetStream, string subjectPrefix, object message, IReadOnlyDictionary<string, string> headers, IMessageSerializer serializer, string replyTo = null,
            CancellationToken cancellationToken = default)
        {
            byte[] data;
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, message);
                data = stream.ToArray();
            }

            var natsHeaders = new NatsHeaders();
            foreach (var header in headers)
            {
                natsHeaders[header.Key] = header.Value;
            }
            if (replyTo != null)
            {
                natsHeaders[ReplyToHeader] = replyTo;
            }

            var ack = await jetStream.PublishAsync($"{subjectPrefix}.{message.GetType().Name}", data, headers: natsHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
            ack.EnsureSuccess();
        }

        public void Dispose()
        {
        }
    }
}
