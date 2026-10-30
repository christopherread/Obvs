using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Obvs.Hosting;
using Obvs.Serialization;
using Obvs.Transports;

namespace Obvs.NATS.JetStream
{
    internal sealed class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        // How long Dispose waits for messages being delivered to finish settling
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

        // How long a receive loop waits before trying again after an error, doubling each time in a row up to the last
        private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);

        // JetStream's error codes for a consumer that doesn't exist, and for a server or account without JetStream
        private const int ConsumerNotFound = 10014;
        private const int JetStreamNotEnabledForAccount = 10039;
        private const int JetStreamNotEnabled = 10076;

        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly INatsJSContext _jetStream;
        private readonly JetStreamTopology _topology;
        private readonly JetStreamTopology.Destination _destination;
        private readonly string _serviceName;
        private readonly NatsJetStreamOptions _options;
        private readonly ConcurrentDictionary<Receiver, bool> _receivers = new ConcurrentDictionary<Receiver, bool>();
        private readonly object _gate = new object();
        private IObservable<TMessage> _shared;
        private TimeSpan _retryDelay;

        public MessageSource(INatsJSContext jetStream, JetStreamTopology topology, JetStreamTopology.Destination destination, string serviceName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers, NatsJetStreamOptions options)
        {
            _jetStream = jetStream;
            _topology = topology;
            _destination = destination;
            _serviceName = serviceName;
            _options = options;
            _deserializers = deserializers.ToDictionary(deserializer => deserializer.GetTypeName());

            Messages = Observable.Defer(Shared);
        }

        public IObservable<TMessage> Messages { get; }

        // One receive loop per consumer, however many subscribe, such as each pending request waiting for its response:
        // loops of their own would take each other's messages. A loop that ended with an error isn't shared any more, as
        // a bus subscribes again as soon as a source reports one, and that starts a new loop.
        private IObservable<TMessage> Shared()
        {
            lock (_gate)
            {
                if (_shared != null)
                {
                    return _shared;
                }

                IObservable<TMessage> shared = null;
                shared = Observable.Create<TMessage>(observer =>
                    {
                        var receiver = new Receiver(this, observer);
                        _receivers[receiver] = true;
                        return Disposable.Create(() =>
                        {
                            _receivers.TryRemove(receiver, out _);
                            receiver.Stop();
                        });
                    })
                    // Hand messages to subscribers on the task pool, in order. Subscriber code running on the receive loop
                    // could otherwise deadlock it, for example by disposing the bus, which waits for that loop. With
                    // MaxConcurrentMessages above 1, messages arrive at the same time, but observers take one at a time.
                    .Synchronize()
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Do(_ => { }, _ => Forget(shared), () => Forget(shared))
                    .Publish()
                    .RefCount();
                return _shared = shared;
            }
        }

        private void Forget(IObservable<TMessage> shared)
        {
            lock (_gate)
            {
                if (_shared == shared)
                {
                    _shared = null;
                }
            }
        }

        // Shared by the source's loops, so a bus subscribing again after an error waits as long as the loop would have
        private TimeSpan RetryDelay
        {
            get
            {
                lock (_gate)
                {
                    return _retryDelay;
                }
            }
        }

        private TimeSpan NextRetryDelay()
        {
            lock (_gate)
            {
                var doubled = TimeSpan.FromTicks(_retryDelay.Ticks * 2);
                return _retryDelay = _retryDelay == TimeSpan.Zero ? FirstRetryDelay : doubled < MaxRetryDelay ? doubled : MaxRetryDelay;
            }
        }

        // Returns whether it was retrying
        private bool ResetRetryDelay()
        {
            lock (_gate)
            {
                var retrying = _retryDelay != TimeSpan.Zero;
                _retryDelay = TimeSpan.Zero;
                return retrying;
            }
        }

        // Once when it starts retrying, not on every retry, and once when it's receiving again
        private void LogRetrying(Exception exception, TimeSpan delay)
        {
            if (delay == FirstRetryDelay)
            {
                _options.Logger.LogWarning(exception, "NATS JetStream can't receive from consumer {Consumer} on stream {Stream}, and retries until it can: {Message}",
                    _destination.Consumer, _destination.Stream, exception.Message);
            }
        }

        private void LogReceivingAgain()
        {
            _options.Logger.LogInformation("NATS JetStream receives from consumer {Consumer} on stream {Stream} again", _destination.Consumer, _destination.Stream);
        }

        // Errors that can pass, such as a lost connection, which the client re-establishes by itself, or a cluster electing
        // a leader. Ones that won't, such as a deleted stream, or a server without JetStream, end the loop.
        private static bool IsTransient(Exception exception)
        {
            switch (exception)
            {
                case NatsJSApiException apiException:
                    return apiException.Error.Code >= 500
                        && apiException.Error.ErrCode != JetStreamNotEnabled
                        && apiException.Error.ErrCode != JetStreamNotEnabledForAccount;
                case NatsException _:
                case TimeoutException _:
                case OperationCanceledException _:
                case IOException _:
                case SocketException _:
                    return true;
                default:
                    return false;
            }
        }

        // Messages of a type this service doesn't know are skipped. The rest carry their headers and settlement
        private TMessage Deserialize(INatsJSMsg<byte[]> received, IMessageSettlement settlement)
        {
            var typeName = received.Subject.Substring(received.Subject.LastIndexOf('.') + 1);
            if (!_deserializers.TryGetValue(typeName, out var deserializer))
            {
                return null;
            }

            TMessage message;
            try
            {
                message = deserializer.Deserialize(new MemoryStream(received.Data ?? new byte[0]));
            }
            catch (Exception exception)
            {
                ObvsTransportTelemetry.RecordUndeserializable(_serviceName, typeName, exception);
                throw;
            }
            var headers = new Dictionary<string, string>();
            if (received.Headers != null)
            {
                foreach (var header in received.Headers)
                {
                    if (header.Key == MessagePublisher<TMessage>.ReplyToHeader)
                    {
                        ReplyAddresses.Set(message, header.Value.ToString());
                    }
                    else
                    {
                        headers[header.Key] = header.Value.ToString();
                    }
                }
            }
            ObvsReceivedMessage.Attach(message, headers, settlement);
            return message;
        }

        // The endpoint closes its connection next, so stop receiving first, even for subscribers still subscribed
        public void Dispose()
        {
            foreach (var receiver in _receivers.Keys)
            {
                receiver.Stop();
            }
        }

        // Pulls one message at a time for each free slot, so a bus takes only what it's ready to handle, and the service's
        // other buses get the rest. A slot is freed once its message is settled.
        private sealed class Receiver
        {
            private readonly MessageSource<TMessage> _source;
            private readonly IObserver<TMessage> _observer;
            private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
            private readonly SemaphoreSlim _slots;
            private readonly ConcurrentDictionary<MessageSettlement, bool> _waiting = new ConcurrentDictionary<MessageSettlement, bool>();
            private readonly ConcurrentDictionary<Task, bool> _delivering = new ConcurrentDictionary<Task, bool>();
            private readonly Task _receiving;
            private volatile bool _stopped;
            private int _stopping;

            public Receiver(MessageSource<TMessage> source, IObserver<TMessage> observer)
            {
                _source = source;
                _observer = observer;
                _slots = new SemaphoreSlim(source._options.MaxConcurrentMessages);
                _receiving = Task.Run(ReceiveAsync);
            }

            // Messages still waiting for a handler stop waiting, so stopping can't wait on a handler that is itself
            // stopping the bus
            public void Stop()
            {
                if (Interlocked.Exchange(ref _stopping, 1) == 1)
                {
                    return;
                }
                _stopped = true;
                _cancellation.Cancel();
                foreach (var settlement in _waiting.Keys)
                {
                    settlement.StopWaiting();
                }
                try
                {
#pragma warning disable VSTHRD002 // IDisposable.Dispose is synchronous
                    Task.WhenAll(_delivering.Keys.Concat(new[] { _receiving })).Wait(StopTimeout);
#pragma warning restore VSTHRD002
                }
                catch (AggregateException)
                {
                }
            }

            // The client reconnects by itself, and sends a pull it was waiting on again, so a lost connection only holds the
            // loop up. Messages it had taken but not settled are delivered again once their AckWait runs out.
            private async Task ReceiveAsync()
            {
                var token = _cancellation.Token;
                try
                {
                    var retryDelay = _source.RetryDelay;
                    if (retryDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(retryDelay, token).ConfigureAwait(false);
                    }

                    INatsJSConsumer consumer = null;
                    while (!token.IsCancellationRequested)
                    {
                        await _slots.WaitAsync(token).ConfigureAwait(false);
                        INatsJSMsg<byte[]> received;
                        try
                        {
                            consumer = consumer ?? await GetConsumerAsync(token).ConfigureAwait(false);
                            received = await PullAsync(consumer, token).ConfigureAwait(false);
                        }
                        catch (Exception exception) when (!token.IsCancellationRequested && IsTransient(exception))
                        {
                            // Fetching the consumer again finds out whether it, or its stream, has gone
                            _slots.Release();
                            consumer = null;
                            var delay = _source.NextRetryDelay();
                            _source.LogRetrying(exception, delay);
                            await Task.Delay(delay, token).ConfigureAwait(false);
                            continue;
                        }
                        catch
                        {
                            _slots.Release();
                            throw;
                        }
                        if (_source.ResetRetryDelay())
                        {
                            _source.LogReceivingAgain();
                        }

                        if (received == null)
                        {
                            _slots.Release();
                            continue;
                        }

                        var delivering = DeliverAsync(received);
                        _delivering[delivering] = true;
                        _ = delivering.ContinueWith(task =>
                        {
                            _delivering.TryRemove(task, out _);
                            _slots.Release();
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    if (!_stopped)
                    {
                        _source.NextRetryDelay();
                        _observer.OnError(exception);
                    }
                }
            }

            // A bus's own consumer deletes itself when the bus hasn't pulled for a while, as when it couldn't reach the
            // server, so it's created again. A consumer the service's buses share is only deleted on purpose.
            private async Task<INatsJSConsumer> GetConsumerAsync(CancellationToken token)
            {
                var destination = _source._destination;
                try
                {
                    return await _source._jetStream.GetConsumerAsync(destination.Stream, destination.Consumer, token).ConfigureAwait(false);
                }
                catch (NatsJSApiException exception) when (exception.Error.ErrCode == ConsumerNotFound && destination.OwnedByBus)
                {
                    return await _source._topology.CreateConsumerAsync(_source._jetStream, destination, token).ConfigureAwait(false);
                }
                catch (NatsNoRespondersException exception)
                {
                    throw new InvalidOperationException(
                        $"Service '{_source._serviceName}' uses NATS JetStream, but the server at {_source._options.Url} doesn't have JetStream enabled any more.",
                        exception);
                }
            }

            // A pull nothing answers returns nothing, as one that expires does: the consumer, its stream or JetStream has
            // gone, so it fails instead
            private static async Task<INatsJSMsg<byte[]>> PullAsync(INatsJSConsumer consumer, CancellationToken token)
            {
                var unanswered = false;
                var opts = new NatsJSNextOpts
                {
                    NotificationHandler = (notification, cancellationToken) =>
                    {
                        if (notification is NatsJSNoRespondersNotification)
                        {
                            unanswered = true;
                        }
                        return Task.CompletedTask;
                    },
                };
                var received = await consumer.NextAsync<byte[]>(opts: opts, cancellationToken: token).ConfigureAwait(false);
                if (received == null && unanswered)
                {
                    throw new NatsNoRespondersException();
                }
                return received;
            }

            private async Task DeliverAsync(INatsJSMsg<byte[]> received)
            {
                var settlement = new MessageSettlement(received, _source._jetStream, _source._serviceName, _source._options);
                TMessage message;
                try
                {
                    message = _source.Deserialize(received, settlement);
                }
                catch (Exception exception)
                {
                    // JetStream delivers it again, and it's dead-lettered after MaxDeliveryAttempts
                    await IgnoreFailureAsync(() => settlement.AbandonAsync(exception)).ConfigureAwait(false);
                    return;
                }
                if (message == null)
                {
                    await IgnoreFailureAsync(settlement.CompleteAsync).ConfigureAwait(false);
                    return;
                }

                _waiting[settlement] = true;
                try
                {
                    _observer.OnNext(message);
                    if (_stopped)
                    {
                        settlement.StopWaiting();
                    }
                    await settlement.WaitAsync().ConfigureAwait(false);
                }
                finally
                {
                    _waiting.TryRemove(settlement, out _);
                }
            }

            // JetStream delivers a message again if settling it fails
            private static async Task IgnoreFailureAsync(Func<Task> settle)
            {
                try
                {
                    await settle().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
