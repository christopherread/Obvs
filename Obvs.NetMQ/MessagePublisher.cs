using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Extensions;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.NetMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : IMessage
    {
        private readonly string _address;
        private readonly IMessageSerializer _serializer;
        private readonly NetMQContext _context;
        private readonly string _topic;
        private readonly IScheduler _scheduler;
        private readonly Lazy<PublisherSocket> _socket;

        public MessagePublisher(string address, IMessageSerializer serializer, NetMQContext context, string topic, IScheduler scheduler)
        {
            _address = address;
            _serializer = serializer;
            _context = context;
            _topic = topic;
            _scheduler = scheduler;

            _socket = new Lazy<PublisherSocket>(() =>
            {
                var socket = _context.CreatePublisherSocket();
                socket.Bind(_address);
                Thread.Sleep(TimeSpan.FromSeconds(1)); // wait for subscribers
                return socket;
            });
        }

        private void Publish(TMessage message)
        {
            _socket.Value.SendToTopic(_topic, message.GetType().Name, _serializer.Serialize(message));
        }

        public Task PublishAsync(TMessage message)
        {
            return Observable.Start(() => Publish(message), _scheduler).ToTask();
        }

        public void Dispose()
        {
            if (_socket.IsValueCreated)
            {
                _socket.Value.Dispose();
            }
        }
    }
}