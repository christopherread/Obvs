using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Obvs.Transports;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests
{
    // ReadEventsAsync<T>(ct) and ReadCommandsAsync<T>(ct): a bus's streams read with await foreach
    public class TestAsyncEnumerableStreams
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly Subject<IEvent> _events = new Subject<IEvent>();
        private readonly Subject<ICommand> _commands = new Subject<ICommand>();
        private readonly IServiceBus _bus;

        public TestAsyncEnumerableStreams()
        {
            var endpointClient = A.Fake<IServiceEndpointClient>();
            var endpoint = A.Fake<IServiceEndpoint>();
            A.CallTo(() => endpointClient.Events).Returns(_events);
            A.CallTo(() => endpoint.Commands).Returns(_commands);
            _bus = new ServiceBus(new[] { endpointClient }, new[] { endpoint });
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldYieldTheEventsOfTheTypeAsked()
        {
            await using var events = _bus.ReadEventsAsync<TestServiceEvent1>(Ct).GetAsyncEnumerator(Ct);
            var first = new TestServiceEvent1();
            var second = new TestServiceEvent1();

            _events.OnNext(new TestServiceEvent2());
            _events.OnNext(first);
            _events.OnNext(second);

            Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(Timeout, Ct));
            Assert.Same(first, events.Current);
            Assert.True(await events.MoveNextAsync().AsTask().WaitAsync(Timeout, Ct));
            Assert.Same(second, events.Current);
        }

        [Fact]
        public async Task ShouldYieldTheCommandsOfTheTypeAsked()
        {
            await using var commands = _bus.ReadCommandsAsync<TestServiceCommand1>(Ct).GetAsyncEnumerator(Ct);
            var command = new TestServiceCommand1();

            _commands.OnNext(new TestServiceCommand2());
            _commands.OnNext(command);

            Assert.True(await commands.MoveNextAsync().AsTask().WaitAsync(Timeout, Ct));
            Assert.Same(command, commands.Current);
        }

        [Fact]
        public async Task ShouldWorkWithAwaitForeach()
        {
            var received = new List<TestServiceEvent1>();
            var reading = Task.Run(async () =>
            {
                await foreach (var ev in _bus.ReadEventsAsync<TestServiceEvent1>(Ct))
                {
                    received.Add(ev);
                    if (received.Count == 2)
                    {
                        break;
                    }
                }
            }, Ct);
            await WaitUntilAsync(() => _events.HasObservers);

            _events.OnNext(new TestServiceEvent1());
            _events.OnNext(new TestServiceEvent1());

            await reading.WaitAsync(Timeout, Ct);
            Assert.Equal(2, received.Count);
            Assert.False(_events.HasObservers);
        }

        [Fact]
        public async Task ShouldStopWhenCancelled()
        {
            using var cancellation = new CancellationTokenSource();
            await using var events = _bus.ReadEventsAsync<TestServiceEvent1>(cancellation.Token).GetAsyncEnumerator(Ct);
            var waiting = events.MoveNextAsync().AsTask();

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Timeout, Ct));
        }

        [Fact]
        public async Task ShouldNotSubscribeUntilEnumerated()
        {
            var events = _bus.ReadEventsAsync<TestServiceEvent1>(Ct);
            Assert.False(_events.HasObservers);

            await using var enumerator = events.GetAsyncEnumerator(Ct);

            Assert.True(_events.HasObservers);
        }

        [Fact]
        public async Task ShouldUnsubscribeWhenDisposed()
        {
            var events = _bus.ReadEventsAsync<TestServiceEvent1>(Ct).GetAsyncEnumerator(Ct);

            await events.DisposeAsync();

            Assert.False(_events.HasObservers);
        }

        [Fact]
        public async Task ShouldEndWithTheStreamsError()
        {
            // The bus's own streams survive their endpoints' errors, so this reads a stream that doesn't
            await using var events = new ObservableAsyncEnumerable<IEvent>(_events, 1024, Ct).GetAsyncEnumerator(Ct);

            _events.OnError(new InvalidOperationException("The orders topic was deleted"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => events.MoveNextAsync().AsTask().WaitAsync(Timeout, Ct));
            Assert.Equal("The orders topic was deleted", exception.Message);
        }

        [Fact]
        public async Task ShouldFailRatherThanDropMessagesWhenTheReaderFallsTooFarBehind()
        {
            await using var events = _bus.ReadEventsAsync<TestServiceEvent1>(Ct, capacity: 2).GetAsyncEnumerator(Ct);

            _events.OnNext(new TestServiceEvent1());
            _events.OnNext(new TestServiceEvent1());
            _events.OnNext(new TestServiceEvent1());

            Assert.True(await events.MoveNextAsync());
            Assert.True(await events.MoveNextAsync());
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => events.MoveNextAsync().AsTask().WaitAsync(Timeout, Ct));
            Assert.Contains("2", exception.Message);
            Assert.Contains("capacity", exception.Message);
            Assert.False(_events.HasObservers);
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
    }
}
