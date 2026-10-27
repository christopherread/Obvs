using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Types;
using Xunit;

namespace Obvs.Mcp.Tests
{
    public interface IOrderMessage : IMessage { }

    public record PlaceOrder(string OrderId) : IOrderMessage, ICommand;

    public record OrderPlaced(string OrderId) : IOrderMessage, IEvent;

    public record GetQuote(string Sku) : IOrderMessage, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public record Quote(string Sku, decimal Price) : IOrderMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    // An agent attached over MCP to a running app's bus
    public class TestObvsMcp
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldOfferTheTools()
        {
            await using var app = await StartAsync();

            var tools = (await app.Client.ListToolsAsync(cancellationToken: Ct)).Select(tool => tool.Name).OrderBy(name => name);

            Assert.Equal(new[] { "describe_service", "list_services", "publish_event", "recent_errors", "request", "send_command", "watch" }, tools);
        }

        [Fact]
        public async Task ShouldListTheServices()
        {
            await using var app = await StartAsync();

            var services = JsonNode.Parse(await CallAsync(app, "list_services"));

            var orders = services.AsArray().Single();
            Assert.Equal("orders", (string)orders["name"]);
            Assert.Equal("InMemory", (string)orders["transport"]);
            Assert.Contains("PlaceOrder", orders["commands"].AsArray().Select(name => (string)name));
            Assert.Contains("OrderPlaced", orders["events"].AsArray().Select(name => (string)name));
        }

        [Fact]
        public async Task ShouldDescribeAService()
        {
            await using var app = await StartAsync();

            var contract = JsonNode.Parse(await CallAsync(app, "describe_service", ("service", "orders")));

            Assert.Equal("3.0.0", (string)contract["asyncapi"]);
            Assert.NotNull(contract["channels"]["orders.commands"]["messages"]["PlaceOrder"]);
        }

        [Fact]
        public async Task ShouldSendACommandAndWatchTheEventItCauses()
        {
            await using var app = await StartAsync();

            await CallAsync(app, "send_command", ("messageType", "PlaceOrder"), ("message", Json(@"{ ""OrderId"": ""o-1"" }")));
            var watched = JsonNode.Parse(await CallAsync(app, "watch", ("messageType", "OrderPlaced")));

            var placed = watched.AsArray().Single();
            Assert.Equal("OrderPlaced", (string)placed["type"]);
            Assert.Equal("orders", (string)placed["service"]);
            Assert.Equal("o-1", (string)placed["message"]["OrderId"]);
        }

        [Fact]
        public async Task ShouldPublishAnEvent()
        {
            await using var app = await StartAsync();

            await CallAsync(app, "publish_event", ("messageType", "OrderPlaced"), ("message", Json(@"{ ""OrderId"": ""o-2"" }")));
            var watched = JsonNode.Parse(await CallAsync(app, "watch", ("messageType", "OrderPlaced")));

            Assert.Equal("o-2", (string)watched.AsArray().Single()["message"]["OrderId"]);
        }

        [Fact]
        public async Task ShouldSendARequestAndReturnTheResponse()
        {
            await using var app = await StartAsync();

            var response = JsonNode.Parse(await CallAsync(app, "request", ("messageType", "GetQuote"), ("message", Json(@"{ ""Sku"": ""sku-1"" }"))));

            Assert.Equal("Quote", (string)response["type"]);
            Assert.Equal(9.99m, (decimal)response["message"]["Price"]);
        }

        [Fact]
        public async Task ShouldRefuseAMessageThatDoesntMatchItsType()
        {
            await using var app = await StartAsync();

            var result = await app.Client.CallToolAsync("send_command",
                new Dictionary<string, object> { ["messageType"] = "PlaceOrder", ["message"] = Json(@"{ ""OrderId"": 42 }") }, cancellationToken: Ct);

            Assert.True(result.IsError == true);
            Assert.Contains("PlaceOrder", Text(result));
        }

        [Fact]
        public async Task ShouldRefuseAMessageTypeTheBusDoesntHave()
        {
            await using var app = await StartAsync();

            var result = await app.Client.CallToolAsync("send_command",
                new Dictionary<string, object> { ["messageType"] = "CancelOrder", ["message"] = Json("{}") }, cancellationToken: Ct);

            Assert.True(result.IsError == true);
            Assert.Contains("CancelOrder", Text(result));
            Assert.Contains("PlaceOrder", Text(result));
        }

        [Fact]
        public async Task ShouldReportRecentHandlerErrors()
        {
            await using var app = await StartAsync();

            await CallAsync(app, "send_command", ("messageType", "PlaceOrder"), ("message", Json(@"{ ""OrderId"": ""fail"" }")));
            var errors = await WaitForAsync(async () =>
            {
                var recent = JsonNode.Parse(await CallAsync(app, "recent_errors")).AsArray();
                return recent.Count > 0 ? recent : null;
            });

            Assert.Contains("Out of stock", errors[0]["error"].ToJsonString());
            Assert.Contains("PlaceOrder", errors[0]["message"].ToJsonString());
        }

        [Fact]
        public async Task ShouldOfferOnlyInspectionWhenSendingIsOff()
        {
            await using var app = await StartAsync(options => options.AllowSending = false);

            var tools = (await app.Client.ListToolsAsync(cancellationToken: Ct)).Select(tool => tool.Name).OrderBy(name => name);

            Assert.Equal(new[] { "describe_service", "list_services", "recent_errors", "watch" }, tools);
        }

        [Fact]
        public async Task ShouldRefuseToStartOutsideDevelopment()
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(environment: "Production"));

            Assert.Contains("Production", exception.Message);
            Assert.Contains("AllowOutsideDevelopment", exception.Message);
        }

        [Fact]
        public async Task ShouldStartOutsideDevelopmentWhenAllowed()
        {
            await using var app = await StartAsync(options => options.AllowOutsideDevelopment = true, environment: "Staging");

            Assert.NotEmpty(await app.Client.ListToolsAsync(cancellationToken: Ct));
        }

        private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

        private static async Task<string> CallAsync(McpApp app, string tool, params (string Name, object Value)[] arguments)
        {
            var result = await app.Client.CallToolAsync(tool, arguments.ToDictionary(a => a.Name, a => a.Value), cancellationToken: Ct);
            Assert.True(result.IsError != true, Text(result));
            return Text(result);
        }

        private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

        private static async Task<T> WaitForAsync<T>(Func<Task<T>> find) where T : class
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            T found;
            while ((found = await find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, Ct);
            }
            return found ?? throw new TimeoutException("Nothing turned up in time.");
        }

        private static async Task<McpApp> StartAsync(Action<ObvsMcpOptions> configure = null, string environment = "Development")
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.WebHost.UseTestServer();
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                .UseInMemory()
                .AddMessages(typeof(PlaceOrder), typeof(OrderPlaced), typeof(GetQuote), typeof(Quote))
                .HandleCommand<PlaceOrder>((command, context, ct) => command.OrderId == "fail"
                    ? throw new InvalidOperationException("Out of stock")
                    : context.PublishAsync(new OrderPlaced(command.OrderId), ct))
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m)))));
            builder.Services.AddObvsMcp(configure);
            var app = builder.Build();
            app.MapObvsMcp();
            await app.StartAsync(Ct);

            var http = app.GetTestServer().CreateClient();
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress, "/obvs/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            }, http, loggerFactory: null, ownsHttpClient: false);
            var client = await McpClient.CreateAsync(transport, cancellationToken: Ct);
            return new McpApp(app, http, client);
        }

        private sealed class McpApp : IAsyncDisposable
        {
            private readonly WebApplication _app;
            private readonly HttpClient _http;

            public McpApp(WebApplication app, HttpClient http, McpClient client)
            {
                _app = app;
                _http = http;
                Client = client;
            }

            public McpClient Client { get; }

            public async ValueTask DisposeAsync()
            {
                await Client.DisposeAsync();
                _http.Dispose();
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
        }
    }
}
