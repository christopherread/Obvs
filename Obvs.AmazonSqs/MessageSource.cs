using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Obvs.Hosting;
using Obvs.Serialization;

namespace Obvs.AmazonSqs
{
    internal sealed class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;
        private readonly string _deadLetterQueueUrl;
        private readonly AmazonSqsOptions _options;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly ConcurrentDictionary<ReceiveLoop, bool> _loops = new ConcurrentDictionary<ReceiveLoop, bool>();
        private readonly bool _queueRecreatedIfMissing;
        private readonly object _sharedLock = new object();
        private IObservable<TMessage> _shared;

        /// <param name="sqs">Shared with the endpoint's other sources and publishers, which dispose it</param>
        /// <param name="options">How many messages to have in hand at once, how long to hide them, and how many times to deliver a failing one</param>
        /// <param name="queueRecreatedIfMissing">
        /// The bus recreates the queue if it goes missing, so receiving waits for it to come back rather than reporting an error
        /// </param>
        public MessageSource(IAmazonSQS sqs, string queueUrl, string deadLetterQueueUrl, IEnumerable<IMessageDeserializer<TMessage>> deserializers, AmazonSqsOptions options,
            bool queueRecreatedIfMissing = false)
        {
            _sqs = sqs;
            _queueUrl = queueUrl;
            _deadLetterQueueUrl = deadLetterQueueUrl;
            _options = options;
            _queueRecreatedIfMissing = queueRecreatedIfMissing;
            _deserializers = deserializers.ToDictionary(deserializer => deserializer.GetTypeName());
            Messages = Observable.Defer(() =>
            {
                lock (_sharedLock)
                {
                    return _shared ?? (_shared = CreateShared());
                }
            });
        }

        public IObservable<TMessage> Messages { get; }

        // One receive loop, shared by the source's subscribers while it has any. A loop each would compete for the
        // queue's messages: every request in flight subscribes to the reply queue, and a response one request's loop
        // received would be lost to the request it belongs to. Once the loop reports an error, the next subscriber
        // starts a new one: the bus subscribes again straight away, and would otherwise get the same error back.
        private IObservable<TMessage> CreateShared()
        {
            IObservable<TMessage> shared = null;
            shared = Observable.Create<TMessage>(observer =>
                {
                    var loop = new ReceiveLoop(this, observer);
                    _loops[loop] = true;
                    loop.Start();
                    return Disposable.Create(() =>
                    {
                        loop.Stop();
                        _loops.TryRemove(loop, out _);
                    });
                })
                // Hand messages to subscribers on the task pool, in order. Subscriber code running on the receive loop's
                // thread could otherwise deadlock it, for example by disposing the bus, which waits for the loop to stop.
                .ObserveOn(TaskPoolScheduler.Default)
                .Do(_ => { }, _ => Forget(shared), () => Forget(shared))
                .Publish()
                .RefCount();
            return shared;
        }

        private void Forget(IObservable<TMessage> shared)
        {
            lock (_sharedLock)
            {
                if (_shared == shared)
                {
                    _shared = null;
                }
            }
        }

        // Messages of a type this service doesn't know are skipped
        private TMessage Deserialize(Message received)
        {
            var attributes = (received.MessageAttributes ?? new Dictionary<string, MessageAttributeValue>())
                .Where(attribute => attribute.Value?.StringValue != null)
                .ToDictionary(attribute => attribute.Key, attribute => attribute.Value.StringValue);
            var decoded = MessageCodec.Decode(received.Body ?? "", attributes);
            if (decoded.TypeName == null || !_deserializers.TryGetValue(decoded.TypeName, out var deserializer))
            {
                return null;
            }

            TMessage message;
            try
            {
                message = deserializer.Deserialize(new MemoryStream(decoded.Body));
            }
            catch (Exception exception)
            {
                ObvsTransportTelemetry.RecordUndeserializable(_options.ServiceName, decoded.TypeName, exception);
                throw;
            }
            if (decoded.Headers.Count > 0)
            {
                ObvsMessageHeaders.Set(message, decoded.Headers);
            }
            if (!string.IsNullOrEmpty(decoded.ReplyTo))
            {
                ReplyAddresses.Set(message, decoded.ReplyTo);
            }
            return message;
        }

        // Stops subscriptions still receiving, so none outlives the endpoint's client
        public void Dispose()
        {
            foreach (var loop in _loops.Keys)
            {
                loop.Stop();
            }
        }

        // Long-polls the queue on a thread of its own, so a slow or blocked thread pool can't stall receiving. It has at
        // most MaxConcurrentCalls messages in hand, each until it's settled, by its handler or as it arrives if no handler
        // takes it.
        private sealed class ReceiveLoop
        {
            // The most SQS returns from one receive, and the longest it long-polls
            private const int MaxMessagesPerReceive = 10;
            private const int WaitTimeSeconds = 20;

            // How long to wait before receiving again after an error the AWS SDK's own retries didn't get past
            private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

            private readonly MessageSource<TMessage> _source;
            private readonly IObserver<TMessage> _observer;
            private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
            private readonly SemaphoreSlim _places;
            private readonly ConcurrentDictionary<MessageSettlement, bool> _inHand = new ConcurrentDictionary<MessageSettlement, bool>();
            private readonly Thread _thread;
            private int _stopped;

            public ReceiveLoop(MessageSource<TMessage> source, IObserver<TMessage> observer)
            {
                _source = source;
                _observer = observer;
                _places = new SemaphoreSlim(source._options.MaxConcurrentCalls);
                _thread = new Thread(Run) { IsBackground = true, Name = $"Obvs.AmazonSqs receive {source._queueUrl}" };
            }

            public void Start() => _thread.Start();

            // Messages still in hand stop waiting for their handlers, which may themselves be stopping the bus. SQS
            // delivers them again once their visibility timeout passes.
            public void Stop()
            {
                if (Interlocked.Exchange(ref _stopped, 1) == 1)
                {
                    return;
                }

                _stopping.Cancel();
                foreach (var settlement in _inHand.Keys)
                {
                    settlement.StopWaiting();
                }
                if (Thread.CurrentThread != _thread)
                {
                    _thread.Join();
                }
            }

            private bool _retrying;

            private void Run()
            {
                var ct = _stopping.Token;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        _places.Wait(ct);
                        var places = 1;
                        while (places < MaxMessagesPerReceive && _places.Wait(0))
                        {
                            places++;
                        }

                        List<Message> messages;
                        try
                        {
                            messages = Receive(places, ct);
                        }
                        catch
                        {
                            _places.Release(places);
                            throw;
                        }
                        if (_retrying)
                        {
                            _retrying = false;
                            _source._options.Logger.LogInformation("Amazon SQS receives from {QueueUrl} again", _source._queueUrl);
                        }
                        if (messages.Count < places)
                        {
                            _places.Release(places - messages.Count);
                        }

                        foreach (var message in messages)
                        {
                            Deliver(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        if (ct.IsCancellationRequested || exception is ObjectDisposedException)
                        {
                            return;
                        }
                        // The bus subscribes again as soon as it's told, so wait first: an error that lasts is reported
                        // once each RetryDelay, not as fast as SQS can refuse
                        if (ct.WaitHandle.WaitOne(RetryDelay))
                        {
                            return;
                        }
                        if (IsPermanent(exception))
                        {
                            _observer.OnError(exception);
                            return;
                        }
                        // Once when it starts retrying, not every RetryDelay
                        if (!_retrying)
                        {
                            _retrying = true;
                            _source._options.Logger.LogWarning(exception, "Amazon SQS couldn't receive from {QueueUrl}, and retries until it can: {Message}",
                                _source._queueUrl, exception.Message);
                        }
                    }
                }
            }

            private List<Message> Receive(int maxMessages, CancellationToken ct)
            {
                var response = _source._sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _source._queueUrl,
                    MaxNumberOfMessages = maxMessages,
                    WaitTimeSeconds = WaitTimeSeconds,
                    VisibilityTimeout = AmazonSqsEntities.VisibilityTimeoutSeconds(_source._options),
                    MessageAttributeNames = new List<string> { "All" },
                    MessageSystemAttributeNames = new List<string> { MessageSettlement.ReceiveCount },
                }, ct).GetAwaiter().GetResult();
                return response.Messages ?? new List<Message>();
            }

            private void Deliver(Message received)
            {
                TMessage message;
                try
                {
                    message = _source.Deserialize(received);
                }
                catch (Exception)
                {
                    // SQS delivers it again straight away, then its redrive policy moves it to the dead-letter queue
                    // after MaxDeliveryAttempts
                    SettleUnseen(() => _source._sqs.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                    {
                        QueueUrl = _source._queueUrl,
                        ReceiptHandle = received.ReceiptHandle,
                        VisibilityTimeout = 0,
                    }));
                    return;
                }
                if (message == null)
                {
                    SettleUnseen(() => _source._sqs.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = _source._queueUrl, ReceiptHandle = received.ReceiptHandle }));
                    return;
                }

                MessageSettlement settlement = null;
                settlement = new MessageSettlement(_source._sqs, _source._queueUrl, _source._deadLetterQueueUrl, received, _source._options.MaxDeliveryAttempts, () =>
                {
                    _inHand.TryRemove(settlement, out _);
                    _places.Release();
                });
                _inHand[settlement] = true;
                ObvsMessageSettlement.Set(message, settlement);
                _observer.OnNext(message);
                if (Volatile.Read(ref _stopped) == 1)
                {
                    settlement.StopWaiting();
                }
            }

            // Settles a message no handler will see, giving back its place whether or not SQS accepts
            private void SettleUnseen(Func<Task> settle)
            {
                Task settling;
                try
                {
                    settling = settle();
                }
                catch (Exception exception)
                {
                    settling = Task.FromException(exception);
                }
                settling.ContinueWith(_ => _places.Release(), TaskScheduler.Default);
            }

            // Errors retrying won't fix, such as a queue that's gone, unless the bus recreates it, or credentials SQS refuses
            private bool IsPermanent(Exception exception)
            {
                if (exception is QueueDoesNotExistException)
                {
                    return !_source._queueRecreatedIfMissing;
                }
                if (!(exception is AmazonServiceException service) || service.Retryable != null)
                {
                    return false;
                }
                var isThrottling = service.ErrorCode != null && service.ErrorCode.IndexOf("Throttl", StringComparison.OrdinalIgnoreCase) >= 0;
                return service.StatusCode >= HttpStatusCode.BadRequest && service.StatusCode < HttpStatusCode.InternalServerError && !isThrottling;
            }
        }
    }
}
