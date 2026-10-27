using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;

namespace Obvs.Samples
{
    // The README's quick start. It needs RabbitMQ, so it's compiled, not run: CommonTasks runs the same code in memory.
    public static class QuickStart
    {
        public static async Task Service(string[] args)
        {
            // snippet: quick-start-service
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))
                    .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                    {
                        // ... place the order, then tell whoever's interested
                        await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
                    })
                    .HandleRequest<GetQuote, Quote>((request, context, ct) =>
                        Task.FromResult(new Quote(request.Sku, 9.99m)))));

            await builder.Build().RunAsync();
            // end-snippet
        }

        public static async Task Client(IHost app, CancellationToken ct)
        {
            // snippet: quick-start-client
            var client = app.Services.GetRequiredService<IServiceBusClient>();

            await client.SendAsync(new PlaceOrder("o-1"), ct);

            var quote = await client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);
            // end-snippet
        }
    }
}
