using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Obvs.NATS.JetStream;
using Obvs.NATS.Tests.Hosting;
using Obvs.Serialization;
using Obvs.Serialization.Json;
using Obvs.Transports;
using Obvs.Types;
using Xunit;

namespace Obvs.NATS.Tests.JetStream
{
    // How a receive loop rides out a lost connection, and which errors end it. NATS is faked: the Explicit tests in
    // TestNatsJetStreamReconnect restart a real server.
    public class TestJetStreamMessageSourceRetry
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private const int StreamNotFound = 10059;
        private const int ConsumerNotFound = 10014;

        private readonly INatsJSContext _jetStream = A.Fake<INatsJSContext>();
        private readonly INatsJSConsumer _consumer = A.Fake<INatsJSConsumer>();
        private readonly NatsJetStreamOptions _options = new NatsJetStreamOptions { Url = "nats://localhost:4222" };
        private readonly RecordingLogger _logger = new RecordingLogger();
        private readonly JetStreamTopology _topology;
        private readonly ConcurrentQueue<DateTime> _pulls = new ConcurrentQueue<DateTime>();
        private readonly ConcurrentQueue<object> _received = new ConcurrentQueue<object>();
        private readonly ConcurrentQueue<Exception> _errors = new ConcurrentQueue<Exception>();
        private ConcurrentQueue<Func<NatsJSNextOpts, CancellationToken, Task<INatsJSMsg<byte[]>>>> _nextPulls;
        private Func<NatsJSNextOpts, CancellationToken, Task<INatsJSMsg<byte[]>>> _thenPull = WaitUntilCancelledAsync;

        public TestJetStreamMessageSourceRetry()
        {
            _options.Logger = _logger;
            _topology = new JetStreamTopology("orders", "orders-worker-1", _options);
            A.CallTo(() => _jetStream.GetConsumerAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Returns(new ValueTask<INatsJSConsumer>(_consumer));
            A.CallTo(() => _jetStream.CreateOrUpdateConsumerAsync(A<string>._, A<ConsumerConfig>._, A<CancellationToken>._))
                .Returns(new ValueTask<INatsJSConsumer>(_consumer));
            Pulls();
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldKeepReceivingAfterAPullFails()
        {
            Pulls(Fail(new NatsException("Connection lost")), Deliver("orders.Commands.PlaceOrder", "o-1"));

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => _received.Count == 1);
            Assert.Equal("o-1", Assert.IsType<PlaceOrder>(_received.Single()).OrderId);
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task ShouldLogOnceWhenItLosesTheServerAndAgainWhenItsBack()
        {
            Pulls(Fail(new NatsException("Connection lost")), Fail(new NatsException("Connection lost")), Deliver("orders.Commands.PlaceOrder", "o-1"));

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => _received.Count == 1);
            Assert.Collection(_logger.Entries,
                entry =>
                {
                    Assert.Equal(LogLevel.Warning, entry.Level);
                    Assert.Contains("Connection lost", entry.Message);
                    Assert.Contains(_topology.Commands.Consumer, entry.Message);
                },
                entry => Assert.Equal(LogLevel.Information, entry.Level));
        }

        [Fact]
        public async Task ShouldCountAMessageItCantDeserialize()
        {
            var counted = new ConcurrentQueue<(string Type, object ErrorType)>();
            using var listener = ConsumedMessagesListener(counted);
            var corrupt = A.Fake<INatsJSMsg<byte[]>>();
            A.CallTo(() => corrupt.Subject).Returns("orders.Commands.PlaceOrder");
            A.CallTo(() => corrupt.Data).Returns(Encoding.UTF8.GetBytes("not json"));
            Pulls((opts, cancellationToken) => Task.FromResult(corrupt));

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => counted.Count > 0);
            var (type, errorType) = counted.First();
            Assert.Equal(nameof(PlaceOrder), type);
            Assert.NotNull(errorType);
        }

        [Fact]
        public async Task ShouldKeepTryingToFetchTheConsumerWhileReconnecting()
        {
            A.CallTo(() => _jetStream.GetConsumerAsync("orders_Commands", "orders", A<CancellationToken>._))
                .Throws(new NatsJSApiNoResponseException()).Twice()
                .Then.Returns(new ValueTask<INatsJSConsumer>(_consumer));
            Pulls(Deliver("orders.Commands.PlaceOrder", "o-1"));

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => _received.Count == 1);
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task ShouldWaitLongerBeforeEachRetry()
        {
            _thenPull = Fail(new NatsException("Connection lost"));

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => _pulls.Count >= 4);
            var pulls = _pulls.ToArray();
            var waits = pulls.Skip(1).Zip(pulls, (pull, previous) => pull - previous).Take(3).ToArray();
            Assert.True(waits[0] >= TimeSpan.FromMilliseconds(100), $"Waited {waits[0]} before retrying.");
            Assert.True(waits[1] > waits[0] && waits[2] > waits[1], $"Waited {string.Join(", ", waits)} between retries.");
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task ShouldStopPromptlyWhileWaitingToRetry()
        {
            _thenPull = Fail(new NatsException("Connection lost"));
            var subscription = Subscribe(Commands());
            await WaitUntilAsync(() => _pulls.Count >= 4);

            var stopping = Stopwatch.StartNew();
            subscription.Dispose();

            Assert.True(stopping.Elapsed < TimeSpan.FromMilliseconds(500), $"Stopping took {stopping.Elapsed}.");
            var pulls = _pulls.Count;
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            Assert.Equal(pulls, _pulls.Count);
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task ShouldReportAnErrorWhenTheStreamIsDeleted()
        {
            GetConsumerThenThrow(new NatsJSApiException(new ApiError { Code = 404, ErrCode = StreamNotFound, Description = "stream not found" }));
            _thenPull = Fail(ConsumerDeleted());

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => !_errors.IsEmpty);
            Assert.Equal(StreamNotFound, Assert.IsType<NatsJSApiException>(_errors.Single()).Error.ErrCode);
        }

        [Fact]
        public async Task ShouldReportAnErrorWhenTheConsumerTheServicesBusesShareIsDeleted()
        {
            GetConsumerThenThrow(new NatsJSApiException(new ApiError { Code = 404, ErrCode = ConsumerNotFound, Description = "consumer not found" }));
            _thenPull = Fail(ConsumerDeleted());

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => !_errors.IsEmpty);
            Assert.Equal(ConsumerNotFound, Assert.IsType<NatsJSApiException>(_errors.Single()).Error.ErrCode);
            A.CallTo(() => _jetStream.CreateOrUpdateConsumerAsync(A<string>._, A<ConsumerConfig>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task ShouldRecreateTheBusesOwnConsumerWhenItIsDeleted()
        {
            // It deletes itself after 5 minutes without a pull, as when the bus can't reach the server for that long
            A.CallTo(() => _jetStream.GetConsumerAsync("orders_Events", "orders-worker-1", A<CancellationToken>._))
                .Returns(new ValueTask<INatsJSConsumer>(_consumer)).Once()
                .Then.Throws(new NatsJSApiException(new ApiError { Code = 404, ErrCode = ConsumerNotFound, Description = "consumer not found" })).Once()
                .Then.Returns(new ValueTask<INatsJSConsumer>(_consumer));
            Pulls(Fail(ConsumerDeleted()), Deliver("orders.Events.OrderPlaced", "o-1"));

            using var subscription = Subscribe(Events());

            await WaitUntilAsync(() => _received.Count == 1);
            Assert.Equal("o-1", Assert.IsType<OrderPlaced>(_received.Single()).OrderId);
            Assert.Empty(_errors);
            A.CallTo(() => _jetStream.CreateOrUpdateConsumerAsync("orders_Events",
                    A<ConsumerConfig>.That.Matches(config => config.DurableName == "orders-worker-1"
                        && config.FilterSubject == "orders.Events.*"
                        && config.InactiveThreshold == TimeSpan.FromMinutes(5)
                        && config.DeliverPolicy == ConsumerConfigDeliverPolicy.New),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ShouldReportAnErrorWhenTheServerNoLongerHasJetStream()
        {
            // A pull nothing answers returns nothing, as one that expires does, so the loop checks the consumer is still there
            GetConsumerThenThrow(new NatsNoRespondersException());
            _thenPull = NoResponders;

            using var subscription = Subscribe(Commands());

            await WaitUntilAsync(() => !_errors.IsEmpty);
            Assert.Contains("JetStream", _errors.Single().Message);
        }

        [Fact]
        public async Task ShouldReceiveAgainWhenResubscribedAfterReportingAnError()
        {
            // A bus subscribes to a source again as soon as it reports an error (CatchAndHandle in Obvs)
            A.CallTo(() => _jetStream.GetConsumerAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Throws(new NatsJSApiException(new ApiError { Code = 404, ErrCode = StreamNotFound, Description = "stream not found" })).Once()
                .Then.Returns(new ValueTask<INatsJSConsumer>(_consumer));
            Pulls(Deliver("orders.Commands.PlaceOrder", "o-1"));

            using var subscription = SubscribeAgainAfterErrors(Commands());

            await WaitUntilAsync(() => _received.Count == 1);
            Assert.Single(_errors);
        }

        [Fact]
        public async Task ShouldWaitLongerBeforeReceivingAgainEachTimeItReportsAnError()
        {
            A.CallTo(() => _jetStream.GetConsumerAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Throws(new NatsJSApiException(new ApiError { Code = 404, ErrCode = StreamNotFound, Description = "stream not found" }));
            var reported = new ConcurrentQueue<DateTime>();

            using var subscription = SubscribeAgainAfterErrors(Commands(), _ => reported.Enqueue(DateTime.UtcNow));

            await WaitUntilAsync(() => reported.Count >= 4);
            var times = reported.ToArray();
            var waits = times.Skip(1).Zip(times, (time, previous) => time - previous).Take(3).ToArray();
            Assert.True(waits[0] >= TimeSpan.FromMilliseconds(100), $"Waited {waits[0]} before receiving again.");
            Assert.True(waits[1] > waits[0] && waits[2] > waits[1], $"Waited {string.Join(", ", waits)} between errors.");
        }

        private Obvs.NATS.JetStream.MessageSource<ICommand> Commands()
        {
            return new Obvs.NATS.JetStream.MessageSource<ICommand>(_jetStream, _topology, _topology.Commands, "orders",
                new IMessageDeserializer<ICommand>[] { new JsonMessageDeserializer<PlaceOrder>() }, _options);
        }

        private Obvs.NATS.JetStream.MessageSource<IEvent> Events()
        {
            return new Obvs.NATS.JetStream.MessageSource<IEvent>(_jetStream, _topology, _topology.Events, "orders",
                new IMessageDeserializer<IEvent>[] { new JsonMessageDeserializer<OrderPlaced>() }, _options);
        }

        // Handlers settle each message, which frees its slot for the next
        private IDisposable Subscribe<T>(IMessageSource<T> source) where T : class
        {
            var subscription = source.Messages.Subscribe(
                message =>
                {
                    _received.Enqueue(message);
                    ObvsReceivedMessage.GetSettlement(message).CompleteAsync();
                },
                _errors.Enqueue);
            return new CompositeDisposable(subscription, source);
        }

        private IDisposable SubscribeAgainAfterErrors<T>(IMessageSource<T> source, Action<Exception> onError = null) where T : class
        {
            var subscription = source.Messages
                .Do(_ => { }, exception =>
                {
                    _errors.Enqueue(exception);
                    onError?.Invoke(exception);
                })
                .Retry()
                .Subscribe(message =>
                {
                    _received.Enqueue(message);
                    ObvsReceivedMessage.GetSettlement(message).CompleteAsync();
                });
            return new CompositeDisposable(subscription, source);
        }

        private void GetConsumerThenThrow(Exception exception)
        {
            A.CallTo(() => _jetStream.GetConsumerAsync(A<string>._, A<string>._, A<CancellationToken>._))
                .Returns(new ValueTask<INatsJSConsumer>(_consumer)).Once()
                .Then.Throws(exception);
        }

        // Each pull takes the next of these, then _thenPull
        private void Pulls(params Func<NatsJSNextOpts, CancellationToken, Task<INatsJSMsg<byte[]>>>[] pulls)
        {
            _nextPulls = new ConcurrentQueue<Func<NatsJSNextOpts, CancellationToken, Task<INatsJSMsg<byte[]>>>>(pulls);
            A.CallTo(() => _consumer.NextAsync(A<INatsDeserialize<byte[]>>._, A<NatsJSNextOpts>._, A<CancellationToken>._))
                .ReturnsLazily((INatsDeserialize<byte[]> serializer, NatsJSNextOpts opts, CancellationToken cancellationToken) =>
                {
                    _pulls.Enqueue(DateTime.UtcNow);
                    var pull = _nextPulls.TryDequeue(out var next) ? next : _thenPull;
                    return new ValueTask<INatsJSMsg<byte[]>>(pull(opts, cancellationToken));
                });
        }

        private static Func<NatsJSNextOpts, CancellationToken, Task<INatsJSMsg<byte[]>>> Fail(Exception exception)
        {
            return (opts, cancellationToken) => Task.FromException<INatsJSMsg<byte[]>>(exception);
        }

        private static Func<NatsJSNextOpts, CancellationToken, Task<INatsJSMsg<byte[]>>> Deliver(string subject, string orderId)
        {
            var message = A.Fake<INatsJSMsg<byte[]>>();
            A.CallTo(() => message.Subject).Returns(subject);
            A.CallTo(() => message.Data).Returns(Encoding.UTF8.GetBytes($"{{\"OrderId\":\"{orderId}\"}}"));
            A.CallTo(() => message.Headers).Returns(null);
            return (opts, cancellationToken) => Task.FromResult(message);
        }

        // What the client tells a pull when nothing answers it: the consumer, its stream or JetStream itself has gone
        private static async Task<INatsJSMsg<byte[]>> NoResponders(NatsJSNextOpts opts, CancellationToken cancellationToken)
        {
            if (opts?.NotificationHandler != null)
            {
                await opts.NotificationHandler(new NatsJSNoRespondersNotification(), cancellationToken);
            }
            return null;
        }

        private static async Task<INatsJSMsg<byte[]>> WaitUntilCancelledAsync(NatsJSNextOpts opts, CancellationToken cancellationToken)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            return null;
        }

        // What a pull in progress throws when its consumer, or the consumer's stream, is deleted
        private static NatsJSProtocolException ConsumerDeleted()
        {
            return new NatsJSProtocolException(409, default, "Consumer Deleted");
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(10, Ct);
            }
        }

        // Measurements for the "orders" service, which only these tests use
        private static System.Diagnostics.Metrics.MeterListener ConsumedMessagesListener(ConcurrentQueue<(string Type, object ErrorType)> counted)
        {
            var listener = new System.Diagnostics.Metrics.MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == Obvs.Hosting.ObvsTelemetry.MeterName && instrument.Name == "messaging.client.consumed.messages")
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                },
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
            {
                var tagged = new Dictionary<string, object>();
                foreach (var tag in tags)
                {
                    tagged[tag.Key] = tag.Value;
                }
                if ((string)tagged["messaging.destination.name"] == "orders")
                {
                    counted.Enqueue(((string)tagged["obvs.message.type"], tagged.TryGetValue("error.type", out var errorType) ? errorType : null));
                }
            });
            listener.Start();
            return listener;
        }

        private sealed class RecordingLogger : ILogger
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new ConcurrentQueue<(LogLevel, string)>();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
