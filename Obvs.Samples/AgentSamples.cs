using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Testing;
using Xunit;

namespace Obvs.Samples
{
    public static class OrdersServiceCollectionExtensions
    {
        // An app's own registration of its bus, which tests reuse
        public static IServiceCollection AddOrdersService(this IServiceCollection services)
        {
            return services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                .UseInMemory()
                .AddMessages(OrderMessages.Types)));
        }
    }

    // docs/mcp.md and docs/contracts.md
#pragma warning disable xUnit1051
    public class AgentSamples
    {
        internal static void Mcp(string[] args)
        {
            // snippet: mcp-setup
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))));
            builder.Services.AddObvsMcp();

            var app = builder.Build();
            app.MapObvsMcp(); // at /obvs/mcp
            app.Run();
            // end-snippet
        }

        internal static void McpOptions(WebApplicationBuilder builder)
        {
            // snippet: mcp-options
            builder.Services.AddObvsMcp(options =>
            {
                options.AllowOutsideDevelopment = true;
                options.AllowSending = false;
            });
            // end-snippet
        }

        internal static void Describe(IHost app)
        {
            // snippet: contracts-describe
            var contracts = app.Services.GetRequiredService<ObvsContracts>();
            string all = contracts.Describe();
            string orders = contracts.Describe("orders");
            // end-snippet
        }

        // snippet: contracts-snapshot
        [Fact]
        public async Task TheOrdersContractHasNoBreakingChanges()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddOrdersService());

            ObvsContractSnapshot.Verify(harness.Services, "contracts/orders.asyncapi.json");
        }
        // end-snippet
    }
#pragma warning restore xUnit1051
}
