using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // End to end through AddObvs and UseRabbitMq against a real broker (RABBITMQ_URI, default amqp://localhost:5672).
    // Each test uses its own service name, so its exchanges and queues don't meet the other tests'.
    public class TestRabbitMqHosting
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendCommandsAndPublishEventsOverRabbitMq()
        {
            using var host = await StartHostAsync("obvs-hosting-commands-events");
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });
            Assert.Equal("o-1", (await command.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);

            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" });
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReturnResponsesOverRabbitMq()
        {
            using var host = await StartHostAsync("obvs-hosting-requests");
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var server = bus.Requests.OfType<GetQuote>()
                .Subscribe(request => _ = bus.ReplyAsync(request, new Quote { Price = 9.99m }));

            var quote = await bus.GetResponses(new GetQuote { Sku = "sku-1" }).OfType<Quote>().FirstAsync().ToTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotCollideWhenTwoBusesInOneProcessUseTheSameService()
        {
            // Both buses subscribe to the same service's commands from one process, as two test hosts would.
            // Each subscription needs its own exclusive queue. Without unique names, the second bus's queue
            // declaration fails with RESOURCE_LOCKED: the bus reports the error and retries under a new name,
            // so the clash shows up as errors, not lost messages.
            using var hostA = await StartHostAsync("obvs-hosting-two-buses");
            using var hostB = await StartHostAsync("obvs-hosting-two-buses");
            var busA = hostA.Services.GetRequiredService<IServiceBus>();
            var busB = hostB.Services.GetRequiredService<IServiceBus>();
            var errors = new ConcurrentQueue<Exception>();
            using var errorsA = busA.Exceptions.Subscribe(errors.Enqueue);
            using var errorsB = busB.Exceptions.Subscribe(errors.Enqueue);

            var receivedByA = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var receivedByB = busB.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await busA.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, TestContext.Current.CancellationToken)).OrderId);
            Assert.Empty(errors);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeadlockWhenASubscriberDisposesTheBus()
        {
            // Code that runs on receiving a message (including code after awaiting one) may shut the app down.
            // If messages arrived on RabbitMQ's consumer-dispatch thread, closing the connection there would
            // wait for that same thread, forever.
            var host = await StartHostAsync("obvs-hosting-dispose-in-callback");
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(_ =>
            {
                host.Dispose();
                disposed.SetResult(true);
            });

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.True(await disposed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        }

        private static async Task<IHost> StartHostAsync(string serviceName)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(serviceName, service => service
                    .UseRabbitMq(options => options.Uri = new Uri(TestBroker.Uri))
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)))));
            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }
    }
}
