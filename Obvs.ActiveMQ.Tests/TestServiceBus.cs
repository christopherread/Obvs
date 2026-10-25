using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;
using Xunit;

namespace Obvs.ActiveMQ.Tests
{
    public class TestServiceBus
    {
        public interface IServiceBusTestMessage : IMessage
        {
        }

        public class TestCommand : ICommand, IServiceBusTestMessage
        {
            public int Id { get; set; }
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenCodeAfterAwaitingAMessageDisposesTheBus()
        {
            // Code after awaiting a message may shut the app down. If messages arrived on the NMS session's
            // dispatch thread, that code would run there too, and stopping the connection would wait for that
            // same thread, forever. (NMS only skips the wait for code in the dispatch's own execution context,
            // which code after an await is not.)
            IServiceBus serviceBus = ServiceBus.Configure()
                    .WithActiveMQEndpoints<IServiceBusTestMessage>()
                        .Named("Obvs.TestService.DisposeAfterAwait")
                        .ConnectToBroker(TestBroker.Uri)
                        .SerializedAsJson()
                        .AsClientAndServer()
                    .Create();
            // Continuations run inline, wherever the message is received
            var received = new TaskCompletionSource<TestCommand>();
            using var subscription = serviceBus.Commands.OfType<TestCommand>().Subscribe(command => received.TrySetResult(command));

            async Task DisposeOnReceiving()
            {
                await received.Task.ConfigureAwait(false);
                ((IDisposable)serviceBus).Dispose();
            }

            var disposed = DisposeOnReceiving();
            await serviceBus.SendAsync(new TestCommand { Id = 1 });

            await disposed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }
}
