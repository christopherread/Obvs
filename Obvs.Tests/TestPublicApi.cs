using System;
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
                "IServiceBusClient",
                "ObservableSubscribeAsyncExtensions",
                "ObvsMessageHeaders",
            }, root);
        }

        [Fact]
        public void ShouldNotBuildTheBusOnGenericInterfaces()
        {
            Assert.Equal(new[] { typeof(IServiceBusClient) }, typeof(IServiceBus).GetInterfaces());
            Assert.Empty(typeof(IServiceBusClient).GetInterfaces());
        }

        // Members, not extension methods, which an agent reading the interface misses
        [Theory]
        [InlineData(typeof(IServiceBusClient), "RequestAsync", 4)]
        [InlineData(typeof(IServiceBusClient), "ReadEventsAsync", 1)]
        [InlineData(typeof(IServiceBusClient), "SendAsync", 3)]
        [InlineData(typeof(IServiceBus), "ReadCommandsAsync", 1)]
        [InlineData(typeof(IServiceBus), "PublishAsync", 2)]
        public void ShouldOfferWhatAppsCallAsMembersOfTheBus(Type bus, string name, int overloads)
        {
            Assert.Equal(overloads, bus.GetMethods().Count(method => method.Name == name));
        }

        [Fact]
        public void ShouldOfferOneWayEachToAwaitAResponseAndToStreamThem()
        {
            // RequestAsync for the response, GetResponses(request) for a stream of them
            var methods = typeof(IServiceBusClient).GetMethods();

            Assert.DoesNotContain(methods, method => method.Name == "GetResponse");
            Assert.All(methods.Where(method => method.Name == "GetResponses"), method => Assert.False(method.IsGenericMethod));
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
