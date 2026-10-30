using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static Obvs.Analyzers.Tests.AnalyzerRun;

namespace Obvs.Analyzers.Tests
{
    public class TestAnalyzers
    {
        private static string Configure(string service) => @"
namespace Contoso.Orders
{
    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.AddObvs(obvs => obvs.AddService<IOrderMessage>(""orders"", " + service + @"));
        }
    }
}";

        private static string UsingBus(string statements) => @"
namespace Contoso.Orders
{
    public static class Worker
    {
        public static void Run(IServiceBus bus)
        {
            " + statements + @"
        }
    }
}";

        // OBVS001: a message registered or handled on a service doesn't implement its message interface

        [Fact]
        public async Task ShouldReportARegisteredMessageOfAnotherService()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(Configure("service => service.UseFake(o => { }).AddMessages(typeof(PlaceOrder), typeof(ShipOrder))")));

            Assert.Equal("OBVS001", diagnostic.Id);
            Assert.Contains("ShipOrder", diagnostic.GetMessage());
            Assert.Contains("IOrderMessage", diagnostic.GetMessage());
            Assert.Contains("docs/analyzers.md#obvs001", diagnostic.Descriptor.HelpLinkUri);
        }

        [Fact]
        public async Task ShouldReportAHandlerForAnotherServicesMessage()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(Configure(
                "service => service.UseFake(o => { }).HandleCommand<ShipOrder>((command, context, ct) => Task.CompletedTask)")));

            Assert.Equal("OBVS001", diagnostic.Id);
            Assert.Contains("ShipOrder", diagnostic.GetMessage());
        }

        [Fact]
        public async Task ShouldAcceptTheServicesOwnMessages()
        {
            Assert.Empty(await DiagnosticsAsync(Configure(
                "service => service.UseFake(o => { }).AddMessages(typeof(PlaceOrder), typeof(OrderPlaced)).HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask)")));
        }

        [Fact]
        public async Task ShouldFixAMessageByAddingTheServicesInterface()
        {
            var source = Configure("service => service.UseFake(o => { }).AddMessages(typeof(Stray))") + @"
namespace Contoso.Orders
{
    public class Stray : ICommand { }
}";

            var fixedSource = await FixAsync(source, "OBVS001");

            Assert.Contains("public class Stray : ICommand, IOrderMessage { }", Normalize(fixedSource));
            Assert.Empty(await DiagnosticsAsync(fixedSource.Substring(Prelude.Length)));
        }

        // OBVS002: more than one kind of message

        [Fact]
        public async Task ShouldReportAMessageThatIsMoreThanOneKind()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(@"
namespace Contoso.Orders
{
    public class PlaceAndAnnounce : IOrderMessage, ICommand, IEvent { }
}"));

            Assert.Equal("OBVS002", diagnostic.Id);
            Assert.Contains("PlaceAndAnnounce", diagnostic.GetMessage());
            Assert.Contains("ICommand", diagnostic.GetMessage());
            Assert.Contains("IEvent", diagnostic.GetMessage());
        }

        [Fact]
        public async Task ShouldReportAMessageThatInheritsASecondKind()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(@"
namespace Contoso.Orders
{
    public class PlacedCommand : OrderPlaced, ICommand { }
}"));

            Assert.Equal("OBVS002", diagnostic.Id);
        }

        // OBVS003: a request handler's response belongs to another service

        [Fact]
        public async Task ShouldReportAResponseOfAnotherService()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(Configure(
                "service => service.UseFake(o => { }).HandleRequest<GetShippingQuote, ShippingQuote>((request, context, ct) => Task.FromResult(new ShippingQuote()))")));

            Assert.Equal("OBVS003", diagnostic.Id);
            Assert.Contains("ShippingQuote", diagnostic.GetMessage());
            Assert.Contains("IOrderMessage", diagnostic.GetMessage());
        }

        // Typed requests: a request handler answers with the response its request declares

        [Fact]
        public async Task ShouldNotCompileAHandlerAnsweringWithAnotherResponse()
        {
            var errors = await AnalyzerRun.CompilerErrorsAsync(Configure(
                "service => service.UseFake(o => { }).HandleRequest<GetQuote, ShippingQuote>((request, context, ct) => Task.FromResult(new ShippingQuote()))"));

            var error = Assert.Single(errors);
            Assert.Equal("CS0311", error.Id);
            Assert.Contains("IRequest<Contoso.Orders.ShippingQuote>", error.GetMessage());
        }

        [Fact]
        public async Task ShouldNotCompileAHandlerForARequestThatDeclaresNoResponse()
        {
            var errors = await AnalyzerRun.CompilerErrorsAsync(@"
namespace Contoso.Orders
{
    public class GetStock : IOrderMessage, IRequest { public string RequestId { get; set; } public string RequesterId { get; set; } }
}" + Configure("service => service.UseFake(o => { }).HandleRequest<GetStock, Quote>((request, context, ct) => Task.FromResult(new Quote()))"));

            Assert.Equal("CS0311", Assert.Single(errors).Id);
        }

        [Fact]
        public async Task ShouldInferTheResponseTypeOfARequest()
        {
            Assert.Empty(await DiagnosticsAsync(UsingBus(
                "Task<Quote> quote = bus.RequestAsync(new GetQuote(), TimeSpan.FromSeconds(5));")));
        }

        // OBVS004: no transport

        [Fact]
        public async Task ShouldReportAServiceWithNoTransport()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(Configure("service => service.HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask)")));

            Assert.Equal("OBVS004", diagnostic.Id);
            Assert.Contains("'orders'", diagnostic.GetMessage());
        }

        [Fact]
        public async Task ShouldAcceptAServiceWithItsOwnEndpointProvider()
        {
            Assert.Empty(await DiagnosticsAsync(Configure("service => service.UseEndpointProvider(provider => null)")));
        }

        [Fact]
        public async Task ShouldOfferEachTransportItFinds()
        {
            var titles = await FixTitlesAsync(Configure("service => { }"), "OBVS004");

            Assert.Equal(new[] { "Add UseFake(...)" }, titles);
        }

        [Fact]
        public async Task ShouldFixAnExpressionLambdaByAddingATransport()
        {
            var fixedSource = await FixAsync(Configure("service => service.AsServer()"), "OBVS004");

            Assert.Contains(@"service => service.AsServer().UseFake(options => { })", Normalize(fixedSource));
            Assert.Empty(await DiagnosticsAsync(fixedSource.Substring(Prelude.Length)));
        }

        [Fact]
        public async Task ShouldFixABlockLambdaByAddingATransport()
        {
            var fixedSource = await FixAsync(Configure("service => { service.AsServer(); }"), "OBVS004");

            Assert.Contains("service => { service.UseFake(options => { }); service.AsServer(); }", Normalize(fixedSource));
            Assert.Empty(await DiagnosticsAsync(fixedSource.Substring(Prelude.Length)));
        }

        // OBVS005: a discarded subscription

        [Fact]
        public async Task ShouldReportADiscardedSubscriptionToABusStream()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(UsingBus("bus.Events.OfType<OrderPlaced>().Subscribe(placed => { });")));

            Assert.Equal("OBVS005", diagnostic.Id);
        }

        [Fact]
        public async Task ShouldAcceptAKeptSubscription()
        {
            Assert.Empty(await DiagnosticsAsync(UsingBus("var subscription = bus.Events.Subscribe(ev => { });")));
        }

        [Fact]
        public async Task ShouldIgnoreObservablesThatArentTheBuses()
        {
            Assert.Empty(await DiagnosticsAsync(UsingBus("Observable.Return(1).Subscribe(value => { });")));
        }

        [Fact]
        public async Task ShouldFixADiscardedSubscriptionByKeepingIt()
        {
            var fixedSource = await FixAsync(UsingBus("bus.Commands.Subscribe(command => { });"), "OBVS005");

            Assert.Contains("var subscription = bus.Commands.Subscribe(command => { });", Normalize(fixedSource));
        }

        [Fact]
        public async Task ShouldReportADiscardedSubscribeAsync()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(UsingBus("bus.Events.OfType<OrderPlaced>().SubscribeAsync(async placed => await Task.Delay(1));")));

            Assert.Equal("OBVS005", diagnostic.Id);
        }

        [Fact]
        public async Task ShouldFixADiscardedSubscribeAsyncByKeepingIt()
        {
            var fixedSource = await FixAsync(UsingBus("bus.Commands.SubscribeAsync(async command => await Task.Delay(1));"), "OBVS005");

            Assert.Contains("var subscription = bus.Commands.SubscribeAsync(async command => await Task.Delay(1));", Normalize(fixedSource));
        }

        [Fact]
        public async Task ShouldAcceptAKeptSubscribeAsync()
        {
            Assert.Empty(await DiagnosticsAsync(UsingBus("var subscription = bus.Events.SubscribeAsync(async (ev, ct) => await Task.Delay(1, ct));")));
        }

        // OBVS006: async void

        [Fact]
        public async Task ShouldReportAnAsyncLambdaSubscribedToABusStream()
        {
            var diagnostic = Assert.Single(await DiagnosticsAsync(UsingBus("var subscription = bus.Events.Subscribe(async ev => await Task.Delay(1));")));

            Assert.Equal("OBVS006", diagnostic.Id);
            Assert.Contains("SubscribeAsync", diagnostic.GetMessage());
            Assert.Contains("HandleEvent", diagnostic.GetMessage());
        }

        [Fact]
        public async Task ShouldFixAnAsyncLambdaBySubscribingAsync()
        {
            var fixedSource = await FixAsync(UsingBus("var subscription = bus.Events.Subscribe(async ev => await Task.Delay(1));"), "OBVS006");

            Assert.Contains("var subscription = bus.Events.SubscribeAsync(async ev => await Task.Delay(1));", Normalize(fixedSource));
            Assert.Empty(await DiagnosticsAsync(fixedSource.Substring(Prelude.Length)));
        }

        [Fact]
        public async Task ShouldFixAnAsyncLambdaWithABlockBySubscribingAsync()
        {
            var fixedSource = await FixAsync(UsingBus("var subscription = bus.Commands.OfType<PlaceOrder>().Subscribe(async command => { await Task.Delay(1); });"), "OBVS006");

            Assert.Contains("var subscription = bus.Commands.OfType<PlaceOrder>().SubscribeAsync(async command => { await Task.Delay(1); });", Normalize(fixedSource));
            Assert.Empty(await DiagnosticsAsync(fixedSource.Substring(Prelude.Length)));
        }
    }
}
