using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Obvs.Generators.Tests
{
    public class TestMessagesGenerator
    {
        private const string Messages = """
            using Obvs.Types;

            namespace Contoso.Orders
            {
                public interface IOrderMessage : IMessage { }

                public class PlaceOrder : IOrderMessage, ICommand { }

                public class OrderPlaced : IOrderMessage, IEvent { }

                public class GetQuote : IOrderMessage, IRequest
                {
                    public string? RequestId { get; set; }
                    public string? RequesterId { get; set; }
                }

                public class Quote : IOrderMessage, IResponse
                {
                    public string? RequestId { get; set; }
                    public string? RequesterId { get; set; }
                }

                public class Invoice { }
            }
            """;

        [Fact]
        public void ShouldListEveryConcreteMessageClassImplementingTheInterface()
        {
            var run = GeneratorRun.Of(Messages + """

                namespace Contoso.Orders
                {
                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }
                }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(
                new[] { "Contoso.Orders.GetQuote", "Contoso.Orders.OrderPlaced", "Contoso.Orders.PlaceOrder", "Contoso.Orders.Quote" },
                run.TypesOf("Contoso.Orders.OrderMessages"));
        }

        [Fact]
        public void ShouldLeaveOutAbstractClassesAndInterfaces()
        {
            var run = GeneratorRun.Of("""
                using Obvs.Types;

                namespace Contoso.Orders
                {
                    public interface IOrderMessage : IMessage { }

                    public interface IOrderCommand : IOrderMessage, ICommand { }

                    public abstract class OrderCommand : IOrderCommand { }

                    public class CancelOrder : OrderCommand { }

                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }
                }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(new[] { "Contoso.Orders.CancelOrder" }, run.TypesOf("Contoso.Orders.OrderMessages"));
        }

        [Fact]
        public void ShouldListNestedMessageClasses()
        {
            var run = GeneratorRun.Of("""
                using Obvs.Types;

                namespace Contoso.Orders
                {
                    public interface IOrderMessage : IMessage { }

                    public static class Commands
                    {
                        public class PlaceOrder : IOrderMessage, ICommand { }
                    }

                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }
                }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(new[] { "Contoso.Orders.Commands+PlaceOrder" }, run.TypesOf("Contoso.Orders.OrderMessages"));
        }

        [Fact]
        public void ShouldListClassesTheBusWillRejectSoItsStartupChecksReportThem()
        {
            // Leaving these out would hide the mistake; listed, the bus says what's wrong with each when it starts
            var run = GeneratorRun.Of("""
                using Obvs.Types;

                namespace Contoso.Orders
                {
                    public interface IOrderMessage : IMessage { }

                    internal class ReindexOrders : IOrderMessage, ICommand { }

                    public class AddOrUpdateOrder : IOrderMessage, ICommand, IEvent { }

                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }
                }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(new[] { "Contoso.Orders.AddOrUpdateOrder", "Contoso.Orders.ReindexOrders" }, run.TypesOf("Contoso.Orders.OrderMessages"));
        }

        [Fact]
        public void ShouldOnlyListClassesFromItsOwnAssembly()
        {
            var shared = GeneratorRun.Library("Contoso.Shared", """
                using Obvs.Types;

                namespace Contoso.Shared
                {
                    public interface IOrderMessage : IMessage { }

                    public class OrderArchived : IOrderMessage, IEvent { }
                }
                """);

            var run = GeneratorRun.Of("""
                using Obvs.Types;
                using Contoso.Shared;

                namespace Contoso.Orders
                {
                    public class PlaceOrder : IOrderMessage, ICommand { }

                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }
                }
                """, shared);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(new[] { "Contoso.Orders.PlaceOrder" }, run.TypesOf("Contoso.Orders.OrderMessages"));
        }

        [Fact]
        public void ShouldListNothingWhenNoClassImplementsTheInterface()
        {
            var run = GeneratorRun.Of("""
                using Obvs.Types;

                namespace Contoso.Orders
                {
                    public interface IOrderMessage : IMessage { }

                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }
                }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Empty(run.TypesOf("Contoso.Orders.OrderMessages"));
        }

        [Fact]
        public void ShouldGenerateForARegistryInTheGlobalNamespace()
        {
            var run = GeneratorRun.Of("""
                using Obvs.Types;

                public interface IOrderMessage : IMessage { }

                public class PlaceOrder : IOrderMessage, ICommand { }

                [ObvsMessages(typeof(IOrderMessage))]
                public partial class OrderMessages { }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(new[] { "PlaceOrder" }, run.TypesOf("OrderMessages"));
        }

        [Fact]
        public void ShouldGenerateEachRegistryForItsOwnInterface()
        {
            var run = GeneratorRun.Of("""
                using Obvs.Types;

                namespace Contoso
                {
                    public interface IOrderMessage : IMessage { }

                    public interface IShippingMessage : IMessage { }

                    public class PlaceOrder : IOrderMessage, ICommand { }

                    public class ShipOrder : IShippingMessage, ICommand { }

                    [ObvsMessages(typeof(IOrderMessage))]
                    public partial class OrderMessages { }

                    [ObvsMessages(typeof(IShippingMessage))]
                    internal partial class ShippingMessages { }
                }
                """);

            Assert.Empty(run.Diagnostics);
            Assert.Equal(new[] { "Contoso.PlaceOrder" }, run.TypesOf("Contoso.OrderMessages"));
            Assert.Equal(new[] { "Contoso.ShipOrder" }, run.TypesOf("Contoso.ShippingMessages"));
        }

        [Fact]
        public void ShouldReportARegistryThatIsntPartial()
        {
            var run = GeneratorRun.Of(Messages + """

                namespace Contoso.Orders
                {
                    [ObvsMessages(typeof(IOrderMessage))]
                    public class OrderMessages { }
                }
                """);

            var error = Assert.Single(run.Errors);
            Assert.Equal("OBVS100", error.Id);
            Assert.Contains("OrderMessages", error.GetMessage());
            Assert.Contains("partial", error.GetMessage());
            Assert.Equal("OrderMessages", run.ErrorSourceText(error));
        }

        [Fact]
        public void ShouldReportARegistryNestedInAnotherClass()
        {
            var run = GeneratorRun.Of(Messages + """

                namespace Contoso.Orders
                {
                    public static partial class Registries
                    {
                        [ObvsMessages(typeof(IOrderMessage))]
                        public partial class OrderMessages { }
                    }
                }
                """);

            var error = Assert.Single(run.Errors);
            Assert.Equal("OBVS101", error.Id);
            Assert.Contains("OrderMessages", error.GetMessage());
            Assert.Contains("Registries", error.GetMessage());
        }
    }
}
