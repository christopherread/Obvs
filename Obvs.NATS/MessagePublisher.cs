using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using Obvs.Serialization;

// v6's NATS transport building blocks, which UseNats(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.NATS
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly INatsConnection _connection;
        private readonly string _subjectPrefix;
        private readonly IMessageSerializer _serializer;
        private readonly Func<TMessage, Dictionary<string, string>> _properties;

        public MessagePublisher(INatsConnection connection,
            string subjectPrefix,
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, string>> properties = null)
        {
            _connection = connection;
            _subjectPrefix = subjectPrefix;
            _serializer = serializer;
            _properties = properties ?? (message => null);
        }

        public async Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
        {
            byte[] body;
            using (var stream = new MemoryStream())
            {
                _serializer.Serialize(stream, message);
                body = stream.ToArray();
            }

            var subject = string.Format("{0}.{1}", _subjectPrefix, message.GetType().Name);
            var wrapper = new MessageWrapper(_properties(message), body);

            using (var stream = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(stream, wrapper);
                await _connection.PublishAsync(subject, stream.ToArray(), GetHeaders(message), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        // The message's Obvs headers (such as trace context) as NATS headers, which v6 receivers ignore
        private static NatsHeaders GetHeaders(TMessage message)
        {
            var headers = ObvsMessageHeaders.Get(message);
            if (headers.Count == 0)
            {
                return null;
            }

            var natsHeaders = new NatsHeaders();
            foreach (var header in headers)
            {
                natsHeaders[header.Key] = header.Value;
            }
            return natsHeaders;
        }

        public void Dispose()
        {
        }
    }
}