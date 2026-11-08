using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Configuration;
using Obvs.NetMQ.Extensions;
using Obvs.Serialization;
using Obvs.Transports;

namespace Obvs.NetMQ
{
    /// <summary>
    /// Publishes messages to a NetMQ topic, as v6 did, for an app that builds its own endpoints. Its first message waits
    /// for a subscriber to the topic, for up to 5 seconds, as ZeroMQ drops messages published before one connects.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
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

        /// <summary>Creates a publisher to <paramref name="topic"/> at <paramref name="address"/>.</summary>
        /// <param name="address">The socket's address, for example <c>tcp://*:5556</c></param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="topic">The topic, for example <c>orders.Events</c></param>
        /// <param name="socketType">Whether the socket binds to the address, by default, or connects to it</param>
        /// <param name="sendHeaders">Whether to send the message's headers, in a fifth frame, which v6 sources can't read</param>
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

        private void Publish(TMessage message, IReadOnlyDictionary<string, string> headers)
        {
            var socket = _socket.Value;

            // Discard subscription frames that arrived since, so they don't build up
            byte[] frame;
            while (socket.TryReceiveFrameBytes(TimeSpan.Zero, out frame))
            {
            }

            socket.SendToTopic(_topic, message.GetType().Name, _serializer.Serialize(message), _sendHeaders ? headers : null);
        }

        /// <inheritdoc />
        public Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Publish(message, headers);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
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