using System.Linq;
using Xunit;

namespace Obvs.Tests
{
    // The root namespace holds what apps use. What transports are built from is in Obvs.Transports, so an app's
    // completion list, and an agent reading the namespace, find the bus and not its plumbing.
    public class TestPublicApi
    {
        private static readonly System.Reflection.Assembly Obvs = typeof(IServiceBus).Assembly;

        [Fact]
        public void ShouldKeepOnlyWhatAppsUseInTheRootNamespace()
        {
            var root = Obvs.GetExportedTypes().Where(type => type.Namespace == "Obvs").Select(type => type.Name).OrderBy(name => name);

            Assert.Equal(new[]
            {
                "IServiceBus",
                "IServiceBus`5",
                "IServiceBusClient",
                "IServiceBusClient`5",
                "ObservableSubscribeAsyncExtensions",
                "ObvsMessageHeaders",
                "ServiceBusAsyncEnumerableExtensions",
                "ServiceBusClientRequestExtensions",
            }, root);
        }

        [Theory]
        [InlineData("IEndpoint")]
        [InlineData("IEndpoint`1")]
        [InlineData("IMessagePublisher`1")]
        [InlineData("DefaultMessagePublisher`1")]
        [InlineData("IMessageSource`1")]
        [InlineData("DefaultMessageSource`1")]
        [InlineData("IServiceEndpoint")]
        [InlineData("IServiceEndpoint`5")]
        [InlineData("ServiceEndpoint")]
        [InlineData("ServiceEndpoint`5")]
        [InlineData("IServiceEndpointClient")]
        [InlineData("IServiceEndpointClient`5")]
        [InlineData("ServiceEndpointClient")]
        [InlineData("ServiceEndpointClient`5")]
        [InlineData("IServiceEndpointProvider")]
        [InlineData("IServiceEndpointProvider`5")]
        [InlineData("ServiceEndpointProviderBase")]
        [InlineData("ServiceEndpointProviderBase`5")]
        [InlineData("MergedMessageSource`1")]
        [InlineData("TypeRoutingMessagePublisher`1")]
        [InlineData("MessagePropertyNames")]
        [InlineData("IMessagePropertyProvider`1")]
        [InlineData("IMessageSettlement")]
        [InlineData("ObvsReceivedMessage")]
        public void ShouldPutWhatTransportsAreBuiltFromInObvsTransports(string name)
        {
            Assert.NotNull(Obvs.GetType("Obvs.Transports." + name));
        }
    }
}
