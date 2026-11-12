using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;
using Obvs.NetMQ.Configuration;
using Obvs.NetMQ.Extensions;
using Obvs.Serialization;
using Obvs.Transports;

namespace Obvs.NetMQ
{
    /// <summary>
    /// Receives the messages published to a NetMQ topic, as v6 did, for an app that builds its own endpoints. Each
    /// subscription has its own subscriber socket, on a thread of its own, for as long as it lasts.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : class
    {
        private readonly string _address;
        private readonly Dictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly string _topic;
        private readonly SocketType _socketType;
        private readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(1);

        /// <summary>Creates a source of the messages published to <paramref name="topic"/> at <paramref name="address"/>.</summary>
        /// <param name="address">The socket's address, for example <c>tcp://localhost:5556</c></param>
        /// <param name="deserializers">The deserializers, one for each type of message, chosen by the type's name the publisher sends</param>
        /// <param name="topic">The topic, for example <c>orders.Events</c></param>
        /// <param name="socketType">Whether the socket connects to the address, by default, or binds to it</param>
        public MessageSource(string address, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string topic, SocketType socketType = SocketType.Client)
        {
            _address = address;
            _deserializers = deserializers.ToDictionary(d => d.TypeName, d => d);
            _topic = topic;
            _socketType = socketType;
        }
        
        /// <inheritdoc />
        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;
                    var subscribedEvent = new ManualResetEventSlim(false);
                    var receiveThreadId = 0;

                    // The receive loop blocks for as long as the subscription lasts, so it gets a thread of its
                    // own: on the thread pool, a busy pool delays it and publishers drop messages meanwhile
                    var task = Task.Factory.StartNew(() =>
                    {
                        Volatile.Write(ref receiveThreadId, Environment.CurrentManagedThreadId);
                        Subscriber(token, observer, subscribedEvent);
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    subscribedEvent.Wait();

                    return Disposable.Create(() =>
                    {
                        tokenSource.Cancel();
                        // A subscriber can unsubscribe as it receives, as FirstAsync does, on the receive thread. That
                        // can't wait for itself: the loop stops once the subscriber returns.
                        if (Environment.CurrentManagedThreadId != Volatile.Read(ref receiveThreadId))
                        {
                            task.Wait();
                        }
                    });
                });
            }
        }

        private void Subscriber(CancellationToken token, IObserver<TMessage> observer, ManualResetEventSlim subscribedEvent)
        {
            try
            {
                using (var socket = new SubscriberSocket())
                {
                    socket.Start(_address, _socketType);
                    socket.Subscribe(_topic);
                    subscribedEvent.Set();
                 
                    while (!token.IsCancellationRequested)
                    {
                        ReceiveMessage(observer, socket);
                    }

                    socket.Unsubscribe(_topic);
                    socket.Stop(_address, _socketType);
                }
            }
            finally
            {
                if (!subscribedEvent.IsSet)
                {
                    subscribedEvent.Set();
                }
            }
        }

        private void ReceiveMessage(IObserver<TMessage> observer, SubscriberSocket socket)
        {
            try
            {
                string topic;
                byte[] rawMessage;
                string typeName;
                IReadOnlyDictionary<string, string> headers;

                if (socket.TryReceive(_receiveTimeout, out topic, out typeName, out rawMessage, out headers) && MatchesTopic(topic))
                {
                    var message = typeName == null
                        ? _deserializers.Values.Single().Deserialize(rawMessage)
                        : _deserializers[typeName].Deserialize(rawMessage);

                    if (headers != null)
                    {
                        ObvsReceivedMessage.Attach(message, headers);
                    }
                    observer.OnNext(message);
                }
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }
        }

        private bool MatchesTopic(string topic)
        {
            return topic == _topic;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
