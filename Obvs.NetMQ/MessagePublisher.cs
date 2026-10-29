using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Configuration;
using Obvs.NetMQ.Extensions;
using Obvs.Serialization;

// v6's NetMQ transport building blocks, which UseNetMq(...) is built on: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

namespace Obvs.NetMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> where TMessage : class
    {
        private readonly string _address;
        private readonly IMessageSerializer _serializer;
        private readonly string _topic;
        private readonly Lazy<XPublisherSocket> _socket;
        private readonly SocketType _socketType;
        private readonly bool _sendHeaders;

        // The first publish gives subscribers at least MinimumSubscriberWait to join, and if none has
        // subscribed to the topic by then, keeps waiting for one up to SubscriberWaitTimeout
        private static readonly TimeSpan MinimumSubscriberWait = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan SubscriberWaitTimeout = TimeSpan.FromSeconds(5);

        // sendHeaders adds a fifth frame with the message's headers, which v6 receivers can't read
        public MessagePublisher(string address, IMessageSerializer serializer, string topic, SocketType socketType = SocketType.Server, bool sendHeaders = false)
        {
            _sendHeaders = sendHeaders;
            _address = address;
            _serializer = serializer;
            _topic = topic;
            _socketType = socketType;

            _socket = new Lazy<XPublisherSocket>(() =>
            {
                var socket = new XPublisherSocket();
                socket.Start(_address, _socketType);
                WaitForSubscriber(socket);
                return socket;
            });
        }

        // ZeroMQ drops messages published before a subscriber has connected (the "slow joiner"). An XPUB
        // socket receives subscriptions, so we can tell when one for our topic has arrived.
        private void WaitForSubscriber(XPublisherSocket socket)
        {
            var start = DateTime.UtcNow;
            var subscribed = false;
            for (var remaining = SubscriberWaitTimeout; !subscribed && remaining > TimeSpan.Zero; remaining = start + SubscriberWaitTimeout - DateTime.UtcNow)
            {
                byte[] frame;
                subscribed = socket.TryReceiveFrameBytes(remaining, out frame) && IsSubscriptionTo(frame, _topic);
            }

            // Give other subscribers the same time to join as before
            var settle = start + MinimumSubscriberWait - DateTime.UtcNow;
            if (settle > TimeSpan.Zero)
            {
                Thread.Sleep(settle);
            }
        }

        // A subscription frame is 1 followed by the subscribed prefix; 0 is an unsubscription
        private static bool IsSubscriptionTo(byte[] frame, string topic)
        {
            return frame.Length > 0 && frame[0] == 1 &&
                   topic.StartsWith(Encoding.UTF8.GetString(frame, 1, frame.Length - 1), StringComparison.Ordinal);
        }

        private void Publish(TMessage message)
        {
            var socket = _socket.Value;

            // Discard subscription frames that arrived since, so they don't build up
            byte[] frame;
            while (socket.TryReceiveFrameBytes(TimeSpan.Zero, out frame))
            {
            }

            socket.SendToTopic(_topic, message.GetType().Name, _serializer.Serialize(message), _sendHeaders ? ObvsMessageHeaders.Get(message) : null);
        }

        public Task PublishAsync(TMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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