using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NATS.Client;
using Obvs.Serialization;

namespace Obvs.NATS
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly Lazy<IConnection> _lazyConnection;
        private readonly string _subjectPrefix;
        private readonly IMessageSerializer _serializer;
        private readonly Func<TMessage, Dictionary<string, string>> _properties;

        public MessagePublisher(Lazy<IConnection> lazyConnection,
            string subjectPrefix,
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, string>> properties = null)
        {
            _lazyConnection = lazyConnection;
            _subjectPrefix = subjectPrefix;
            _serializer = serializer;
            _properties = properties ?? (message => null);
        }

        public Task PublishAsync(TMessage message)
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
                _lazyConnection.Value.Publish(subject, stream.ToArray());
            }
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }
    }
}