using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using Obvs.Serialization;
using Obvs.Transports;

namespace Obvs.NATS
{
    /// <summary>
    /// Publishes messages to NATS, as v6 did, for an app that builds its own endpoints. Each is published to the subject
    /// <c>{subjectPrefix}.{type's name}</c>, with its headers as NATS headers.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly INatsConnection _connection;
        private readonly string _subjectPrefix;
        private readonly IMessageSerializer _serializer;
        private readonly Func<TMessage, Dictionary<string, string>> _properties;

        /// <summary>Creates a publisher to the subjects under <paramref name="subjectPrefix"/>.</summary>
        /// <param name="connection">The connection to NATS</param>
        /// <param name="subjectPrefix">The subjects' prefix, for example <c>orders.Events</c></param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="properties">The properties to send with each message, which sources can filter on. None when null</param>
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

        /// <inheritdoc />
        public async Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
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
                await _connection.PublishAsync(subject, stream.ToArray(), ToNatsHeaders(headers), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        // The Obvs headers (such as trace context) as NATS headers, which v6 receivers ignore
        private static NatsHeaders ToNatsHeaders(IReadOnlyDictionary<string, string> headers)
        {
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

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}