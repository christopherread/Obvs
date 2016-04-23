using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using Obvs.NetMQ.Configuration;
using Obvs.NetMQ.Extensions;
using Obvs.Serialization;

namespace Obvs.NetMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly string _address;
        private readonly IMessageSerializer _serializer;
        private readonly string _topic;
        private readonly Lazy<PublisherSocket> _socket;
        private readonly SocketType _socketType;

        public MessagePublisher(string address, IMessageSerializer serializer, string topic, SocketType socketType = SocketType.Server)
        {
            _address = address;
            _serializer = serializer;
            _topic = topic;
            _socketType = socketType;

            _socket = new Lazy<PublisherSocket>(() =>
            {
	            var socket = new PublisherSocket();
                socket.Start(_address, _socketType);
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
            Publish(message);
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            if (_socket.IsValueCreated)
            {
                _socket.Value.Stop(_address, _socketType);
                _socket.Value.Dispose();
            }
        }
    }
}