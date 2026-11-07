using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;
using Xunit;
using static Obvs.RabbitMQ.Tests.Hosting.RabbitMqServices;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // Which of a service's buses gets each message, and how many each takes at once
    public class TestRabbitMqDelivery : IClassFixture<RabbitMqServices>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly RabbitMqServices _services;

        public TestRabbitMqDelivery(RabbitMqServices services)
        {
            _services = services;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleEachCommandOnceAcrossTheServicesBuses()
        {
            var serviceName = _services.Create("shared-commands");
            var handled = new ConcurrentQueue<string>();
            using var serverA = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => { handled.Enqueue(command.OrderId); return Task.CompletedTask; }));
            using var serverB = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => { handled.Enqueue(command.OrderId); return Task.CompletedTask; }));
            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            var bus = client.Services.GetRequiredService<IServiceBusClient>();

            for (var i = 0; i < 10; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, Ct);
            }

            await WaitUntilAsync(() => handled.Count >= 10);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"o-{i}").OrderBy(id => id), handled.OrderBy(id => id));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldAnswerEachRequestOnceAcrossTheServicesBuses()
        {
            var serviceName = _services.Create("shared-requests");
            var answered = 0;
            Func<GetQuote, MessageContext, CancellationToken, Task<Quote>> answer = (request, context, ct) =>
            {
                Interlocked.Increment(ref answered);
                return Task.FromResult(new Quote { Price = 9.99m });
            };
            using var serverA = await StartHostAsync(serviceName, service => service.AsServer().HandleRequest(answer));
            using var serverB = await StartHostAsync(serviceName, service => service.AsServer().HandleRequest(answer));
            using var client = await StartHostAsync(serviceName, service => service.AsClient());

            var quote = await client.Services.GetRequiredService<IServiceBusClient>().RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, Ct);

            Assert.Equal(9.99m, quote.Price);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(1, Volatile.Read(ref answered));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepACommandSentWhileNoServerIsRunning()
        {
            var serviceName = _services.Create("no-server");
            var stopped = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            await stopped.StopAsync(Ct);
            stopped.Dispose();
            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            await client.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);

            var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var server = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.TrySetResult(command.OrderId);
                    return Task.CompletedTask;
                }));

            Assert.Equal("o-1", await handled.Task.WaitAsync(Timeout, Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldNotDeclareTheSharedQueuesForAClient()
        {
            // Its service's server may still be on v6, which doesn't read them, so they'd fill up, and a v7 server would
            // later handle what's on them again
            var serviceName = _services.Create("client-only");

            using var client = await StartHostAsync(serviceName, service => service.AsClient());
            await client.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);

            Assert.Null(await _services.ReadyMessagesAsync($"{serviceName}.Commands"));
            Assert.Null(await _services.ReadyMessagesAsync($"{serviceName}.Requests"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverEventsToEveryBus()
        {
            var serviceName = _services.Create("every-bus");
            using var server = await StartHostAsync(serviceName, service => service.AsServer());
            using var clientA = await StartHostAsync(serviceName, service => service.AsClient());
            using var clientB = await StartHostAsync(serviceName, service => service.AsClient());

            var receivedByA = clientA.Services.GetRequiredService<IServiceBusClient>().Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            var receivedByB = clientB.Services.GetRequiredService<IServiceBusClient>().Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await server.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, Ct);

            Assert.Equal("o-1", (await receivedByA.WaitAsync(Timeout, Ct)).OrderId);
            Assert.Equal("o-1", (await receivedByB.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleAsManyCommandsAtOnceAsTheirHandlersMaxConcurrency()
        {
            var running = new ConcurrencyRecorder();
            using var host = await StartHostAsync(_services.Create("concurrent"), service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 4));
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 4; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, Ct);
            }

            await WaitUntilAsync(() => running.Current == 4);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldHandleNoMoreCommandsAtOnceThanMaxConcurrentMessages()
        {
            var running = new ConcurrencyRecorder();
            using var host = await StartHostAsync(_services.Create("capped"), service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync(), handler => handler.MaxConcurrency = 4),
                options: options => options.MaxConcurrentMessages = 2);
            var bus = host.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 4; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, Ct);
            }

            await WaitUntilAsync(() => running.Current == 2);
            await Task.Delay(TimeSpan.FromSeconds(3), Ct);
            Assert.Equal(2, running.Most);
            running.Finish();
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldLeaveCommandsBeyondMaxConcurrentMessagesOnTheQueueForTheServicesOtherBuses()
        {
            // Without a limit, the broker would push every command to the first bus to connect
            var serviceName = _services.Create("prefetch");
            var running = new ConcurrencyRecorder();
            using var busy = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => running.RunAsync()));
            var bus = busy.Services.GetRequiredService<IServiceBus>();

            for (var i = 0; i < 3; i++)
            {
                await bus.SendAsync(new PlaceOrder { OrderId = $"o-{i}" }, Ct);
            }
            await WaitUntilAsync(() => running.Current == 1);

            var handledElsewhere = new ConcurrentQueue<string>();
            using var idle = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => { handledElsewhere.Enqueue(command.OrderId); return Task.CompletedTask; }));

            await WaitUntilAsync(() => handledElsewhere.Count == 2);
            running.Finish();
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
                await Task.Delay(100, Ct);
            }
        }

        // Counts handlers running at once, holding each until Finish
        private sealed class ConcurrencyRecorder
        {
            private readonly TaskCompletionSource<bool> _finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _current;
            private int _most;

            public int Current => Volatile.Read(ref _current);

            public int Most => Volatile.Read(ref _most);

            public async Task RunAsync()
            {
                var now = Interlocked.Increment(ref _current);
                int most;
                while ((most = Volatile.Read(ref _most)) < now && Interlocked.CompareExchange(ref _most, now, most) != most)
                {
                }
                try
                {
                    await _finish.Task;
                }
                finally
                {
                    Interlocked.Decrement(ref _current);
                }
            }

            public void Finish() => _finish.TrySetResult(true);
        }
    }
}
