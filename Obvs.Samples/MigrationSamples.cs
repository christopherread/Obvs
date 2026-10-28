using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Configuration;
using Obvs.Hosting;
using Obvs.RabbitMQ.Configuration;
using Obvs.Serialization.Json.Configuration;

namespace Obvs.Samples
{
    // docs/migrating-from-v6.md's before and after. The v6 code is what the analyzers warn about.
#pragma warning disable OBVS005, OBVS006
    public static class MigrationSamples
    {
        public static IServiceBus V6Configuration()
        {
            // snippet: v6-configuration
            IServiceBus bus = ServiceBus.Configure()
                .WithRabbitMQEndpoints<IOrderMessage>()
                    .Named("orders")
                    .ConnectToBroker("amqp://localhost:5672")
                    .SerializedAsJson()
                    .AsClientAndServer()
                .UsingConsoleLogging()
                .Create();
            // end-snippet
            return bus;
        }

        public static void V7Configuration(HostApplicationBuilder builder)
        {
            // snippet: v7-configuration
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))));

            // IServiceBus and IServiceBusClient are in the container. The bus starts with the host.
            // end-snippet
        }

        public static async Task V7WithoutAHost()
        {
            // snippet: v7-without-a-host
            await using var bus = await ObvsBus.StartAsync(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))));
            // end-snippet
        }

        public static void V6Handling(IServiceBus bus)
        {
            // snippet: v6-handling
            bus.Commands.OfType<PlaceOrder>().Subscribe(async command =>
            {
                await bus.PublishAsync(new OrderPlaced(command.OrderId));
            });
            // end-snippet
        }

        public static void V7Handling(ObvsServiceBuilder service)
        {
            // snippet: v7-handling
            service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
            {
                await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
            });

            // Or a class from the container
            service.HandleCommand<PlaceOrder, PlaceOrderHandler>();
            // end-snippet
        }

        public static async Task<Quote> V6Request(IServiceBus bus)
        {
            // snippet: v6-request
            var quote = await bus.GetResponses(new GetQuote("sku-1"))
                .OfType<Quote>().Take(1).Timeout(TimeSpan.FromSeconds(5)).ToTask();
            // end-snippet
            return quote;
        }

        public static async Task<Quote> V7Request(IServiceBusClient client, CancellationToken ct)
        {
            // snippet: v7-request
            var quote = await client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);
            // end-snippet
            return quote;
        }
    }
#pragma warning restore OBVS005, OBVS006

    public static class AnalyzerSamples
    {
        public static IDisposable RunInTurn(IServiceBus bus)
        {
            // snippet: obvs006-fixed
            var subscription = bus.Events.OfType<OrderPlaced>()
                .Select(placed => Observable.FromAsync(async () => await ShipAsync(placed)))
                .Concat()
                .Subscribe();
            // end-snippet
            return subscription;
        }

        private static Task ShipAsync(OrderPlaced placed) => Task.CompletedTask;
    }
}
