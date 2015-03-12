using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Obvs.NetMQ.Extensions;
using Obvs.Types;

namespace Obvs.NetMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : IMessage
    {
        private readonly string _address;
        private readonly Dictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly NetMQContext _context;
        private readonly string _topic;
        private readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(1);

        public MessageSource(string address, IEnumerable<IMessageDeserializer<TMessage>> deserializers, NetMQContext context, string topic)
        {
            _address = address;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName(), d => d);
            _context = context;
            _topic = topic;
        }
        
        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    CancellationTokenSource tokenSource = new CancellationTokenSource();
                    CancellationToken token = tokenSource.Token;
                    ManualResetEventSlim subscribedEvent = new ManualResetEventSlim(false);

                    Task task = Task.Run(() => Subscriber(token, observer, subscribedEvent));

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
                using (SubscriberSocket socket = _context.CreateSubscriberSocket())
                {
                    socket.Connect(_address);
                    socket.Subscribe(_topic);
                    subscribedEvent.Set();
                 
                    while (!token.IsCancellationRequested)
                    {
                        ReceiveMessage(observer, socket);
                    }

                    socket.Unsubscribe(_topic);
                    socket.Disconnect(_address);
                    socket.Close();
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
                object rawMessage;
                string typeName;

                if (socket.TryReceive(_receiveTimeout, out topic, out typeName, out rawMessage) && MatchesTopic(topic))
                {
                    TMessage message = typeName == null
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
