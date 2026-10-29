using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Xunit;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // A v6 app, Obvs.V6InteropApp, on the published 6.x packages with Newtonsoft JSON, and a v7 app, configured with AddObvs
    // and its System.Text.Json default, on one broker: each must understand the other, so apps can move to v7 one at a time.
    // The v6 app runs in its own process, as it would in production.
    public class TestV6Interop
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRunTheV6AppOnObvs6()
        {
            await using var v6Server = await V6App.StartServerAsync("obvs-interop-version", TestBroker.Uri, Ct);

            Assert.StartsWith("6.", v6Server.ObvsVersion);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSendFromV7ToAV6ServerAndReceiveItsEvents()
        {
            const string service = "obvs-interop-v7-to-v6";
            await using var v6Server = await V6App.StartServerAsync(service, TestBroker.Uri, Ct);
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
            await using var v6Server = await V6App.StartServerAsync(service, TestBroker.Uri, Ct);
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

            var placed = await V6App.SendCommandAsync(service, TestBroker.Uri, "o-2", Timeout, Ct);

            Assert.Equal("o-2", placed);
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
