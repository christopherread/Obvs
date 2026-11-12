using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using Obvs.Serialization;
using Obvs.Transports;

namespace Obvs.NATS
{
    /// <summary>
    /// Receives the messages published to NATS subjects under a prefix, as v6 did, for an app that builds its own
    /// endpoints. Each subscription has its own NATS subscription to <c>{subjectPrefix}.*</c>.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly INatsConnection _connection;
        private readonly Func<Dictionary<string, string>, bool> _filter;
        private readonly string _subject;

        /// <summary>Creates a source of the messages published under <paramref name="subjectPrefix"/>.</summary>
        /// <param name="connection">The connection to NATS</param>
        /// <param name="subjectPrefix">The subjects' prefix, for example <c>orders.Events</c></param>
        /// <param name="deserializers">The deserializers, one for each type of message, chosen by the subject's last part</param>
        /// <param name="filter">Which messages to receive, by the properties their publisher sent with them. All of them when null</param>
        public MessageSource(INatsConnection connection,
            string subjectPrefix,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> filter = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.TypeName);
            _connection = connection;
            _filter = filter;
            _subject = string.Format("{0}.*", subjectPrefix);
        }

        /// <inheritdoc />
        public IObservable<TMessage> Messages
        {
            get
            {
#pragma warning disable VSTHRD002 // IObservable.Subscribe and IDisposable.Dispose are synchronous
                return Observable.Create<TMessage>(observer =>
                {
                    // Block until the server has the subscription, so messages published straight after
                    // Subscribe returns aren't missed. NATS.Net doesn't capture the synchronization context,
                    // so blocking here can't deadlock.
                    var subscription = _connection.SubscribeCoreAsync<byte[]>(_subject).AsTask().GetAwaiter().GetResult();
                    var cancellation = new CancellationTokenSource();
                    // ReceiveAsync reports its own failures to the observer
                    _ = Task.Run(() => ReceiveAsync(subscription, observer, cancellation.Token));

                    return Disposable.Create(() =>
                    {
                        cancellation.Cancel();
                        subscription.DisposeAsync().AsTask().GetAwaiter().GetResult();
                        cancellation.Dispose();
                    });
                });
#pragma warning restore VSTHRD002
            }
        }

        private async Task ReceiveAsync(INatsSub<byte[]> subscription, IObserver<TMessage> observer, CancellationToken token)
        {
            try
            {
                while (await subscription.Msgs.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    NatsMsg<byte[]> msg;
                    while (subscription.Msgs.TryRead(out msg))
                    {
                        var wrapper = Unwrap(msg.Data);
                        if (MatchesFilter(wrapper))
                        {
                            var message = Deserialize(msg.Subject, wrapper);
                            SetHeaders(message, msg.Headers);
                            observer.OnNext(message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }
        }

        private static void SetHeaders(TMessage message, NatsHeaders natsHeaders)
        {
            if (natsHeaders == null || natsHeaders.Count == 0)
            {
                return;
            }

            var headers = new Dictionary<string, string>();
            foreach (var header in natsHeaders)
            {
                headers[header.Key] = header.Value.ToString();
            }
            ObvsReceivedMessage.Attach(message, headers);
        }

        private bool MatchesFilter(MessageWrapper wrapper)
        {
            return _filter == null || _filter(wrapper.Properties);
        }

        private static MessageWrapper Unwrap(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return ProtoBuf.Serializer.Deserialize<MessageWrapper>(stream);
            }
        }

        private TMessage Deserialize(string subject, MessageWrapper wrapper)
        {
            IMessageDeserializer<TMessage> deserializer;
            var messageType = GetMessageType(subject);

            if (!_deserializers.TryGetValue(messageType, out deserializer))
            {
                if (_deserializers.Count > 1)
                {
                    throw new Exception(string.Format("Missing deserializer for type '{0}'", messageType));
                }
                
                deserializer = _deserializers.Values.Single();
            }
            return deserializer.Deserialize(new MemoryStream(wrapper.Body));
        }

        private static string GetMessageType(string subject)
        {
            return subject.Split('.').Last();
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
