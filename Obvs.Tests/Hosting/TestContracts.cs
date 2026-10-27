using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Tests.Hosting.Handlers;
using Xunit;

namespace Obvs.Tests.Hosting
{
    // Each service's contract, as an AsyncAPI document with a JSON Schema for each message
    public class TestContracts
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldDescribeAServicesMessagesAsAsyncApi()
        {
            await using var harness = await StartAsync(service => service
                .AddMessages(typeof(PlaceOrder), typeof(OrderPlaced), typeof(GetQuote), typeof(Quote)));

            var document = JsonNode.Parse(harness.Services.GetRequiredService<ObvsContracts>().Describe());

            Assert.Equal("3.0.0", (string)document["asyncapi"]);
            Assert.Equal(new[] { "PlaceOrder" }, MessageNames(document, "orders.commands"));
            Assert.Equal(new[] { "OrderPlaced" }, MessageNames(document, "orders.events"));
            Assert.Equal(new[] { "GetQuote" }, MessageNames(document, "orders.requests"));
            Assert.Equal(new[] { "Quote" }, MessageNames(document, "orders.responses"));
            Assert.Equal("orders", (string)document["channels"]["orders.commands"]["x-obvs-service"]);
            Assert.Equal("InMemory", (string)document["channels"]["orders.commands"]["x-obvs-transport"]);

            var payload = Message(document, "orders.commands", "PlaceOrder")["payload"];
            Assert.Contains("string", payload["properties"]["OrderId"]["type"].ToJsonString());
        }

        [Fact]
        public async Task ShouldDescribeWhatTheAppSendsAndReceives()
        {
            await using var harness = await StartAsync(service => service
                .AsClient()
                .AddMessages(typeof(PlaceOrder), typeof(OrderPlaced), typeof(GetQuote), typeof(Quote)));

            var operations = JsonNode.Parse(harness.Services.GetRequiredService<ObvsContracts>().Describe())["operations"].AsObject()
                .ToDictionary(operation => (string)operation.Value["channel"]["$ref"], operation => (string)operation.Value["action"]);

            Assert.Equal("send", operations["#/channels/orders.commands"]);
            Assert.Equal("receive", operations["#/channels/orders.events"]);
            Assert.Equal("send", operations["#/channels/orders.requests"]);
            Assert.Equal("receive", operations["#/channels/orders.responses"]);
        }

        [Fact]
        public async Task ShouldFindAServicesMessagesWhenItDoesntRegisterThem()
        {
            await using var harness = await StartAsync(service => { });

            var document = JsonNode.Parse(harness.Services.GetRequiredService<ObvsContracts>().Describe());

            Assert.Contains("PlaceOrder", MessageNames(document, "orders.commands"));
            Assert.DoesNotContain("ShipOrder", MessageNames(document, "orders.commands"));
        }

        [Fact]
        public async Task ShouldDescribeOneService()
        {
            await using var harness = await ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory().AddMessages(typeof(PlaceOrder)))
                .AddService<IShippingMessage>("shipping", service => service.UseInMemory().AddMessages(typeof(ShipOrder)))),
                cancellationToken: Ct);

            var document = JsonNode.Parse(harness.Services.GetRequiredService<ObvsContracts>().Describe("shipping"));

            Assert.Equal(new[] { "shipping.commands" }, document["channels"].AsObject().Select(channel => channel.Key));
        }

        [Fact]
        public void ShouldFindNoBreakingChangesWhenAMessageGainsAProperty()
        {
            var before = Document(@"{ ""type"": ""object"", ""properties"": { ""OrderId"": { ""type"": ""string"" } } }");
            var after = Document(@"{ ""type"": ""object"", ""properties"": { ""OrderId"": { ""type"": ""string"" }, ""Notes"": { ""type"": ""string"" } } }");

            Assert.Empty(ObvsContractComparer.FindBreakingChanges(before, after));
        }

        [Fact]
        public void ShouldReportARemovedProperty()
        {
            var before = Document(@"{ ""type"": ""object"", ""properties"": { ""OrderId"": { ""type"": ""string"" }, ""Notes"": { ""type"": ""string"" } } }");
            var after = Document(@"{ ""type"": ""object"", ""properties"": { ""OrderId"": { ""type"": ""string"" } } }");

            var change = Assert.Single(ObvsContractComparer.FindBreakingChanges(before, after));
            Assert.Contains("PlaceOrder", change);
            Assert.Contains("Notes", change);
            Assert.Contains("removed", change);
        }

        [Fact]
        public void ShouldReportAChangedPropertyType()
        {
            var before = Document(@"{ ""type"": ""object"", ""properties"": { ""Quantity"": { ""type"": ""integer"" } } }");
            var after = Document(@"{ ""type"": ""object"", ""properties"": { ""Quantity"": { ""type"": ""string"" } } }");

            var change = Assert.Single(ObvsContractComparer.FindBreakingChanges(before, after));
            Assert.Contains("Quantity", change);
            Assert.Contains("integer", change);
            Assert.Contains("string", change);
        }

        [Fact]
        public void ShouldReportANewlyRequiredProperty()
        {
            var before = Document(@"{ ""type"": ""object"", ""properties"": { ""OrderId"": { ""type"": ""string"" } } }");
            var after = Document(@"{ ""type"": ""object"", ""properties"": { ""OrderId"": { ""type"": ""string"" } }, ""required"": [ ""OrderId"" ] }");

            var change = Assert.Single(ObvsContractComparer.FindBreakingChanges(before, after));
            Assert.Contains("OrderId", change);
            Assert.Contains("required", change);
        }

        [Fact]
        public void ShouldReportARemovedMessage()
        {
            var before = Document(@"{ ""type"": ""object"" }");
            var after = @"{ ""asyncapi"": ""3.0.0"", ""channels"": { ""orders.commands"": { ""messages"": { } } }, ""components"": { ""messages"": { } } }";

            var change = Assert.Single(ObvsContractComparer.FindBreakingChanges(before, after));
            Assert.Contains("PlaceOrder", change);
            Assert.Contains("orders.commands", change);
        }

        // A document with one channel and message, whose payload is schema
        private static string Document(string schema) => @"{
  ""asyncapi"": ""3.0.0"",
  ""channels"": { ""orders.commands"": { ""messages"": { ""PlaceOrder"": { ""$ref"": ""#/components/messages/Contoso.PlaceOrder"" } } } },
  ""components"": { ""messages"": { ""Contoso.PlaceOrder"": { ""name"": ""PlaceOrder"", ""payload"": " + schema + @" } } }
}";

        private static string[] MessageNames(JsonNode document, string channel)
        {
            return document["channels"][channel]["messages"].AsObject().Select(message => message.Key).OrderBy(name => name).ToArray();
        }

        private static JsonNode Message(JsonNode document, string channel, string name)
        {
            var reference = ((string)document["channels"][channel]["messages"][name]["$ref"]).Substring("#/components/messages/".Length);
            return document["components"]["messages"][reference];
        }

        private static Task<ObvsTestHarness> StartAsync(System.Action<ObvsServiceBuilder> configure)
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => configure(service.UseInMemory()))),
                cancellationToken: Ct);
        }
    }
}
