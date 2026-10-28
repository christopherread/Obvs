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
using Xunit;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // A v6 app, configured with the fluent ServiceBus.Configure() chain and Newtonsoft JSON, and a v7 app, configured
    // with AddObvs and its System.Text.Json default, on one broker: each must understand the other, so apps can move
    // to v7 one at a time.
    public class TestV6Interop
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendFromV7ToAV6ServerAndReceiveItsEvents()
        {
            const string service = "obvs-interop-v7-to-v6";
            var v6Server = V6(service, server: true);
            using var v6Disposal = (IDisposable)v6Server;
            using var handling = v6Server.Commands.OfType<PlaceOrder>()
                .Subscribe(command => _ = v6Server.PublishAsync(new OrderPlaced { OrderId = command.OrderId }));
            using var v7Client = await StartV7Async(service, configure => configure.AsClient());
            var v7 = v7Client.Services.GetRequiredService<IServiceBusClient>();
            var placed = v7.Events.OfType<OrderPlaced>().FirstAsync().ToTask();

            await v7.SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);

            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, Ct)).OrderId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldGetAV6ServersResponseToAV7Request()
        {
            const string service = "obvs-interop-v7-requests";
            var v6Server = V6(service, server: true);
            using var v6Disposal = (IDisposable)v6Server;
            using var answering = v6Server.Requests.OfType<GetQuote>()
                .Subscribe(request => _ = v6Server.ReplyAsync(request, new Quote { Price = 9.99m }));
            using var v7Client = await StartV7Async(service, configure => configure.AsClient());

            var quote = await v7Client.Services.GetRequiredService<IServiceBusClient>()
                .RequestAsync(new GetQuote { Sku = "sku-1" }, Timeout, Ct);

            Assert.Equal(9.99m, quote.Price);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendFromV6ToAV7HandlerAndReceiveItsEvents()
        {
            const string service = "obvs-interop-v6-to-v7";
            using var v7Server = await StartV7Async(service, configure => configure
                .AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced { OrderId = command.OrderId }, ct)));
            var v6Client = V6(service, server: false);
            using var v6Disposal = (IDisposable)v6Client;
            var placed = v6Client.Events.OfType<OrderPlaced>().FirstAsync().ToTask();

            await v6Client.SendAsync(new PlaceOrder { OrderId = "o-2" }, Ct);

            Assert.Equal("o-2", (await placed.WaitAsync(Timeout, Ct)).OrderId);
        }

        private static IServiceBus V6(string service, bool server)
        {
            var endpoint = ServiceBus.Configure()
                .WithRabbitMQEndpoints<IOrderMessage>()
                    .Named(service)
                    .ConnectToBroker(TestBroker.Uri)
                    .SerializedAsJson();
            return server ? endpoint.AsServer().Create() : endpoint.AsClient().Create();
        }

        private static async Task<IHost> StartV7Async(string service, Action<ObvsServiceBuilder> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(service, s => configure(s.UseRabbitMq(options => options.Uri = new Uri(TestBroker.Uri)))));
            var host = builder.Build();
            await host.StartAsync(Ct);
            return host;
        }
    }
}
