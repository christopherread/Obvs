using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Testing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Obvs.Samples
{
    // snippet: class-handler
    public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrder>
    {
        private readonly IOrderStore _orders;

        public PlaceOrderHandler(IOrderStore orders) => _orders = orders;

        public async Task HandleAsync(PlaceOrder command, MessageContext context, CancellationToken ct)
        {
            await _orders.SaveAsync(command.OrderId, ct);
            await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
        }
    }
    // end-snippet

    public interface IOrderStore
    {
        Task SaveAsync(string orderId, CancellationToken ct);
    }

    public sealed class InMemoryOrderStore : IOrderStore
    {
        public ConcurrentQueue<string> Saved { get; } = new ConcurrentQueue<string>();

        public Task SaveAsync(string orderId, CancellationToken ct)
        {
            Saved.Enqueue(orderId);
            return Task.CompletedTask;
        }
    }

    // docs/common-tasks.md, run as tests against the in-memory transport
    public class CommonTasks
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        internal static void AddAService(HostApplicationBuilder builder)
        {
            // snippet: add-a-service
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))
                    .AddMessages(OrderMessages.Types)));
            // end-snippet
        }

        [Fact]
        public async Task HandleACommandAndPublishAnEvent()
        {
            var ct = Ct;
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service =>
                {
                    service.UseInMemory();
                    // snippet: handle-a-command
                    service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
                    {
                        await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
                    });
                    // end-snippet
                })), cancellationToken: ct);
            var client = harness.Client;

            // snippet: send-a-command
            await client.SendAsync(new PlaceOrder("o-1"), ct);
            // end-snippet

            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-1");
        }

        [Fact]
        public async Task HandleACommandWithAClass()
        {
            var store = new InMemoryOrderStore();
            await using var harness = await ObvsTestHarness.StartAsync(services =>
            {
                // snippet: register-class-handler
                services.AddSingleton<IOrderStore>(store);
                services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder, PlaceOrderHandler>()));
                // end-snippet
            }, cancellationToken: Ct);

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Ct);

            await harness.Published<OrderPlaced>(placed => placed.OrderId == "o-1");
            Assert.Equal(new[] { "o-1" }, store.Saved);
        }

        [Fact]
        public async Task HandleEvents()
        {
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service =>
                {
                    service.UseInMemory();
                    // snippet: handle-events
                    service.HandleEvent<OrderPlaced>((placed, context, ct) =>
                    {
                        Console.WriteLine($"Order {placed.OrderId} was placed");
                        received.TrySetResult(placed.OrderId);
                        return Task.CompletedTask;
                    });
                    // end-snippet
                })), cancellationToken: Ct);
            var bus = harness.Bus;
            var ct = Ct;

            // snippet: publish-an-event
            await bus.PublishAsync(new OrderPlaced("o-1"), ct);
            // end-snippet

            Assert.Equal("o-1", await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        }

        [Fact]
        public async Task RequestAndResponse()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service =>
                {
                    service.UseInMemory();
                    // snippet: handle-a-request
                    service.HandleRequest<GetQuote, Quote>(async (request, context, ct) =>
                    {
                        var price = await Task.FromResult(9.99m); // look the price up
                        return new Quote(request.Sku, price);
                    });
                    // end-snippet
                })), cancellationToken: Ct);
            var client = harness.Client;
            var ct = Ct;

            // snippet: make-a-request
            var quote = await client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);
            // end-snippet

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact]
        public async Task ReadEventsAtYourOwnPace()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory())), cancellationToken: Ct);
            var client = harness.Client;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var ct = cancellation.Token;
            var reading = Task.Run(async () =>
            {
                // snippet: read-events
                await foreach (var placed in client.ReadEventsAsync<OrderPlaced>(ct))
                {
                    Console.WriteLine($"Order {placed.OrderId} was placed");
                    break; // or carry on until ct is cancelled
                }
                // end-snippet
            }, Ct);
            while (!reading.IsCompleted)
            {
                await harness.Bus.PublishAsync(new OrderPlaced("o-1"), Ct);
                await Task.WhenAny(reading, Task.Delay(50, Ct));
            }

            await reading;
        }

        [Fact]
        public async Task UseTheBusWithoutAHost()
        {
            var ct = Ct;

            // snippet: without-a-host
            await using var bus = await ObvsBus.StartAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseInMemory()
                    .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced(command.OrderId), ct))), ct);

            await bus.SendAsync(new PlaceOrder("o-1"), ct);
            // end-snippet
        }

        internal static void TraceIt(HostApplicationBuilder builder)
        {
            // snippet: trace-it
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(ObvsTelemetry.ActivitySourceName).AddConsoleExporter())
                .WithMetrics(metrics => metrics.AddMeter(ObvsTelemetry.MeterName).AddConsoleExporter());
            // end-snippet
        }

        internal static void HandleErrors(HostApplicationBuilder builder)
        {
            // snippet: handle-errors
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseAzureServiceBus(options =>
                    {
                        options.FullyQualifiedNamespace = "contoso.servicebus.windows.net";
                        options.MaxDeliveryAttempts = 5; // then it's dead-lettered
                    })
                    .HandleCommand<PlaceOrder>((command, context, ct) =>
                        // Throwing abandons the command, so it's delivered again
                        throw new InvalidOperationException("Out of stock"))));
            // end-snippet
        }
    }
}
