using System;
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

        public MessagePublisher(Lazy<IConnection> lazyConnection,
            string subjectPrefix,
            IMessageSerializer serializer)
        {
            _lazyConnection = lazyConnection;
            _subjectPrefix = subjectPrefix;
            _serializer = serializer;
        }

        public Task PublishAsync(TMessage message)
        {
            using (var stream = new MemoryStream())
            {
                _serializer.Serialize(stream, message);
                var subject = string.Format("{0}.{1}", _subjectPrefix, message.GetType().Name);
                _lazyConnection.Value.Publish(subject, stream.ToArray());
            }
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }
    }
}