using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Extensions;
using Obvs.Types;

namespace Obvs.NetMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : IMessage
    {
        private readonly string _address;
        private readonly IMessageSerializer _serializer;
        private readonly NetMQContext _context;
        private readonly string _topic;

        private readonly BlockingCollection<TMessage> _queue = new BlockingCollection<TMessage>();

        public MessagePublisher(string address, IMessageSerializer serializer, NetMQContext context, string topic)
        {
            _address = address;
            _serializer = serializer;
            _context = context;
            _topic = topic;

            Task.Run(() => Publisher());
        }

        public void Publish(TMessage message)
        {
            _queue.Add(message);
        }

        private void Publisher()
        {
            using (PublisherSocket socket = _context.CreatePublisherSocket())
            {
                socket.Bind(_address);
                Thread.Sleep(TimeSpan.FromSeconds(1)); // wait for subscribers
                PublishQueuedMessages(socket);
            }
        }

        private void PublishQueuedMessages(PublisherSocket socket)
        {
            _queue.GetConsumingEnumerable().ToObservable().Subscribe(msg => Send(msg, socket));
        }

        private void Send(TMessage message, PublisherSocket socket)
        {
            socket.SendToTopic(_topic, message.GetType().Name, _serializer.Serialize(message));
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
        }
    }
}