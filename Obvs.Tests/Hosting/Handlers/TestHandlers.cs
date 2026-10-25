using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obvs.Hosting;
using Obvs.Testing;
using Xunit;

namespace Obvs.Tests.Hosting.Handlers
{
    public class TestHandlers
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldHandleCommandsWithADelegate()
        {
            await using var harness = await StartOrdersAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced(command.OrderId), ct)));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-1");
        }

        [Fact]
        public async Task ShouldReplyWithTheRequestHandlersResponse()
        {
            await using var harness = await StartOrdersAsync(service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))));

            var quote = await harness.Client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), Timeout, Ct);

            Assert.Equal(new Quote("sku-1", 9.99m), quote with { RequestId = null, RequesterId = null });
        }

        [Fact]
        public async Task ShouldHandleEventsAsAClient()
        {
            var handled = new TaskCompletionSource<OrderPlaced>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var harness = await StartOrdersAsync(service => service
                .HandleEvent<OrderPlaced>((placed, context, ct) =>
                {
                    handled.TrySetResult(placed);
                    return Task.CompletedTask;
                }));

            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));

            Assert.Equal("o-1", (await handled.Task.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact]
        public async Task ShouldResolveClassHandlersInANewScopePerMessage()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services
                .AddScoped<ScopeMarker>()
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder, PlaceOrderHandler>())),
                cancellationToken: Ct);

            await harness.Client.SendAsync(new PlaceOrder("o-1"));
            await harness.Client.SendAsync(new PlaceOrder("o-2"));
            var first = await harness.Published<OrderPlaced>(placed => placed.OrderId.StartsWith("o-1:"));
            var second = await harness.Published<OrderPlaced>(placed => placed.OrderId.StartsWith("o-2:"));

            // Each message gets its own scope, and the handler's context exposes that same scope
            var firstScope = first.OrderId.Split(':');
            var secondScope = second.OrderId.Split(':');
            Assert.NotEqual(firstScope[1], secondScope[1]);
            Assert.Equal("same-scope", firstScope[2]);
            Assert.Equal("same-scope", secondScope[2]);
        }

        [Fact]
        public async Task ShouldHandleOneMessageAtATimeInOrderByDefault()
        {
            var running = 0;
            var maxRunning = 0;
            await using var harness = await StartOrdersAsync(service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    RecordConcurrency(ref running, ref maxRunning);
                    await Task.Delay(20, ct);
                    Interlocked.Decrement(ref running);
                    await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
                }));
            var ids = Enumerable.Range(1, 5).Select(i => $"o-{i}").ToArray();

            foreach (var id in ids)
            {
                await harness.Client.SendAsync(new PlaceOrder(id));
            }
            await harness.Published<OrderPlaced>(placed => placed.OrderId == ids.Last());

            Assert.Equal(1, maxRunning);
            Assert.Equal(ids, harness.Messages.OfType<OrderPlaced>().Select(placed => placed.OrderId));
        }

        [Fact]
        public async Task ShouldHandleUpToMaxConcurrencyMessagesAtOnce()
        {
            // Each handler waits until three are running at once: with a limit below three they would all
            // wait and time out; with no limit, all six would run together
            var running = 0;
            var maxRunning = 0;
            var threeRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var harness = await StartOrdersAsync(service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    if (RecordConcurrency(ref running, ref maxRunning) == 3)
                    {
                        threeRunning.TrySetResult(true);
                    }
                    await threeRunning.Task.WaitAsync(Timeout, ct);
                    Interlocked.Decrement(ref running);
                    await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
                }, options => options.MaxConcurrency = 3));

            for (var i = 1; i <= 6; i++)
            {
                await harness.Client.SendAsync(new PlaceOrder($"o-{i}"));
            }
            for (var i = 1; i <= 6; i++)
            {
                await harness.Published<OrderPlaced>(placed => placed.OrderId == $"o-{i}");
            }

            Assert.Equal(3, maxRunning);
        }

        [Fact]
        public async Task ShouldKeepHandlingAfterAHandlerThrows()
        {
            await using var harness = await StartOrdersAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => command.OrderId == "o-1"
                    ? throw new InvalidOperationException("boom")
                    : context.PublishAsync(new OrderPlaced(command.OrderId), ct)));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));
            await harness.Client.SendAsync(new PlaceOrder("o-2"));

            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-2");
        }

        [Fact]
        public async Task ShouldLogHandlerErrorsWithTheServiceAndMessage()
        {
            var logs = new ListLoggerProvider();
            await using var harness = await ObvsTestHarness.StartAsync(services => services
                .AddLogging(logging => logging.AddProvider(logs))
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder>((command, context, ct) => throw new InvalidOperationException("boom")))),
                cancellationToken: Ct);

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var error = await WaitForAsync(() => logs.Entries.FirstOrDefault(entry => entry.Level == LogLevel.Error));
            Assert.IsType<InvalidOperationException>(error.Exception);
            Assert.Contains("orders", error.Message);
            Assert.Contains("PlaceOrder", error.Message);
        }

        [Fact]
        public async Task ShouldFinishRunningHandlersWhenTheHostStops()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finished = false;
            using var host = BuildOrdersHost(service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await release.Task;
                    finished = true;
                }));
            await host.StartAsync(Ct);
            await host.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder("o-1"));
            await started.Task.WaitAsync(Timeout, Ct);

            var stopping = host.StopAsync(Ct);
            await Task.Delay(100, Ct);
            Assert.False(stopping.IsCompleted, "The host stopped while a handler was still running");

            release.SetResult(true);
            await stopping.WaitAsync(Timeout, Ct);
            Assert.True(finished);
        }

        [Fact]
        public async Task ShouldCancelRunningHandlersWhenStoppingTimesOut()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = BuildOrdersHost(service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    try
                    {
                        await Task.Delay(System.Threading.Timeout.Infinite, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled.TrySetResult(true);
                        throw;
                    }
                }));
            await host.StartAsync(Ct);
            await host.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder("o-1"));
            await started.Task.WaitAsync(Timeout, Ct);

            using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await host.StopAsync(shutdownTimeout.Token).WaitAsync(Timeout, Ct);

            Assert.True(await cancelled.Task.WaitAsync(Timeout, Ct));
        }

        [Fact]
        public async Task ShouldFailToStartWhenACommandHandlerIsOnAClientOnlyService()
        {
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => StartOrdersAsync(service => service
                .AsClient()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask)));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("PlaceOrder", error);
            Assert.Contains("AsServer()", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenAnEventHandlerIsOnAServerOnlyService()
        {
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => StartOrdersAsync(service => service
                .AsServer()
                .HandleEvent<OrderPlaced>((placed, context, ct) => Task.CompletedTask)));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("OrderPlaced", error);
            Assert.Contains("AsClient()", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenAHandlerIsForAnotherServicesMessage()
        {
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => StartOrdersAsync(service => service
                .HandleCommand<ShipOrder>((command, context, ct) => Task.CompletedTask)));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("ShipOrder", error);
            Assert.Contains("IOrderMessage", error);
        }

        private static Task<ObvsTestHarness> StartOrdersAsync(Action<ObvsServiceBuilder> configure)
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => configure(service.UseInMemory()))),
                cancellationToken: Ct);
        }

        private static IHost BuildOrdersHost(Action<ObvsServiceBuilder> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => configure(service.UseInMemory())));
            return builder.Build();
        }

        private static int RecordConcurrency(ref int running, ref int maxRunning)
        {
            var now = Interlocked.Increment(ref running);
            int max;
            while (now > (max = Volatile.Read(ref maxRunning)) && Interlocked.CompareExchange(ref maxRunning, now, max) != max)
            {
            }
            return now;
        }

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, Ct);
            }
            return found ?? throw new TimeoutException("The expected log entry wasn't written in time.");
        }

        public sealed class ScopeMarker
        {
            public string Id { get; } = Guid.NewGuid().ToString("N");
        }

        public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrder>
        {
            private readonly ScopeMarker _scope;

            public PlaceOrderHandler(ScopeMarker scope)
            {
                _scope = scope;
            }

            public Task HandleAsync(PlaceOrder command, MessageContext context, CancellationToken cancellationToken)
            {
                var sameScope = ReferenceEquals(_scope, context.Services.GetRequiredService<ScopeMarker>()) ? "same-scope" : "other-scope";
                return context.PublishAsync(new OrderPlaced($"{command.OrderId}:{_scope.Id}:{sameScope}"), cancellationToken);
            }
        }
    }
}
