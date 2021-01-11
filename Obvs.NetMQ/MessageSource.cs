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

namespace Obvs.NetMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : class
    {
        private readonly string _address;
        private readonly Dictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly string _topic;
        private readonly SocketType _socketType;
        private readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(1);

        public MessageSource(string address, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string topic, SocketType socketType = SocketType.Client)
        {
            _address = address;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName(), d => d);
            _topic = topic;
            _socketType = socketType;
        }
        
        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;
                    var subscribedEvent = new ManualResetEventSlim(false);

                    var task = Task.Run(() => Subscriber(token, observer, subscribedEvent));

                    subscribedEvent.Wait();

                    return Disposable.Create(() =>
                    {
                        tokenSource.Cancel();
                        task.Wait();
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

                if (socket.TryReceive(_receiveTimeout, out topic, out typeName, out rawMessage) && MatchesTopic(topic))
                {
                    var message = typeName == null
                        ? _deserializers.Values.Single().Deserialize(rawMessage)
                        : _deserializers[typeName].Deserialize(rawMessage);

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

        public void Dispose()
        {
        }
    }
}
