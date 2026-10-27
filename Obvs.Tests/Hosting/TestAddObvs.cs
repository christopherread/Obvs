using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Configuration;
using Obvs.Hosting;
using Obvs.Serialization;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting
{
    public class TestAddObvs
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Fact]
        public void ShouldResolveOneBusAsBothServiceBusAndClient()
        {
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider))));

            var bus = host.Services.GetRequiredService<IServiceBus>();

            Assert.Same(bus, host.Services.GetRequiredService<IServiceBus>());
            Assert.Same(bus, host.Services.GetRequiredService<IServiceBusClient>());
        }

        [Fact]
        public async Task ShouldCreateTheBusWhenTheHostStarts()
        {
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider))));

            Assert.Equal(0, provider.EndpointsCreated + provider.ClientsCreated);

            await host.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, provider.EndpointsCreated);
            Assert.Equal(1, provider.ClientsCreated);
        }

        [Fact]
        public async Task ShouldDeliverCommandsFromClientToServer()
        {
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider))));
            await host.StartAsync(TestContext.Current.CancellationToken);

            var received = host.Services.GetRequiredService<IServiceBus>()
                .Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await host.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            var command = await received.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("o-1", command.OrderId);
        }

        [Fact]
        public async Task ShouldAddServicesFromRepeatedCallsToOneBus()
        {
            var orders = new FakeEndpointProvider(typeof(IOrderMessage));
            var shipping = new FakeEndpointProvider(typeof(IShippingMessage));
            using var host = BuildHost(services => services
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => orders)))
                .AddObvs(obvs => obvs.AddService<IShippingMessage>("shipping", service => service.UseEndpointProvider(_ => shipping))));
            await host.StartAsync(TestContext.Current.CancellationToken);

            Assert.Same(host.Services.GetRequiredService<IServiceBus>(), host.Services.GetRequiredService<IServiceBusClient>());
            Assert.Equal(1, orders.EndpointsCreated);
            Assert.Equal(1, shipping.EndpointsCreated);
        }

        [Fact]
        public async Task ShouldOnlyCreateClientEndpointsForAClientService()
        {
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider).AsClient())));

            await host.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, provider.EndpointsCreated);
            Assert.Equal(1, provider.ClientsCreated);
        }

        [Fact]
        public async Task ShouldOnlyCreateServerEndpointsForAServerService()
        {
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider).AsServer())));

            await host.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, provider.EndpointsCreated);
            Assert.Equal(0, provider.ClientsCreated);
        }

        [Fact]
        public async Task ShouldDisposeTheBusWhenTheContainerIsDisposed()
        {
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider))));
            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.False(provider.Endpoint.Messages.IsDisposed);

            host.Dispose();

            Assert.True(provider.Endpoint.Messages.IsDisposed);
        }

        [Fact]
        public async Task ShouldDisposeTheBusOnlyOnce()
        {
            // Server only, so the shared fake endpoint is owned once by the bus
            var provider = new FakeEndpointProvider(typeof(IOrderMessage));
            var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider).AsServer())));
            await host.StartAsync(TestContext.Current.CancellationToken);
            host.Services.GetRequiredService<IServiceBusClient>();
            await host.StopAsync(TestContext.Current.CancellationToken);

            host.Dispose();

            Assert.Equal(1, provider.Endpoint.DisposeCount);
        }

        [Fact]
        public async Task ShouldGiveEachHostItsOwnBus()
        {
            // One provider per container, as a real transport's connection would be
            Action<IServiceCollection> configure = services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => new FakeEndpointProvider(typeof(IOrderMessage)))));
            using var hostA = BuildHost(configure);
            using var hostB = BuildHost(configure);
            await hostA.StartAsync(TestContext.Current.CancellationToken);
            await hostB.StartAsync(TestContext.Current.CancellationToken);

            var busA = hostA.Services.GetRequiredService<IServiceBus>();
            var busB = hostB.Services.GetRequiredService<IServiceBus>();
            Assert.NotSame(busA, busB);

            var receivedByB = busB.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var receivedByA = busA.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            await hostA.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await receivedByA.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.False(receivedByB.IsCompleted);
        }

        [Fact]
        public void ShouldResolveAKeyedBusSeparatelyFromTheDefaultBus()
        {
            using var host = BuildHost(services => services
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseEndpointProvider(_ => new FakeEndpointProvider(typeof(IOrderMessage)))))
                .AddObvs("analytics", obvs => obvs.AddService<IShippingMessage>("shipping", service => service
                    .UseEndpointProvider(_ => new FakeEndpointProvider(typeof(IShippingMessage))))));

            var defaultBus = host.Services.GetRequiredService<IServiceBus>();
            var analyticsBus = host.Services.GetRequiredKeyedService<IServiceBus>("analytics");

            Assert.NotSame(defaultBus, analyticsBus);
            Assert.Same(analyticsBus, host.Services.GetRequiredKeyedService<IServiceBusClient>("analytics"));
        }

        [Fact]
        public async Task ShouldFailToStartWhenAServiceHasNoTransport()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => { })));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("UseEndpointProvider", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenTwoServicesShareAName()
        {
            using var host = BuildHost(services => services
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseEndpointProvider(_ => new FakeEndpointProvider(typeof(IOrderMessage)))))
                .AddObvs(obvs => obvs.AddService<IShippingMessage>("orders", service => service
                    .UseEndpointProvider(_ => new FakeEndpointProvider(typeof(IShippingMessage))))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("more than once", error);
        }

        [Fact]
        public async Task ShouldReportEveryConfigurationProblemAtOnce()
        {
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => { })
                .AddService<IShippingMessage>("shipping", service => { })));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Equal(2, exception.Errors.Count);
            Assert.Contains(exception.Errors, error => error.Contains("'orders'"));
            Assert.Contains(exception.Errors, error => error.Contains("'shipping'"));
        }

        [Fact]
        public async Task ShouldGiveEachBusItsOwnInstanceId()
        {
            string instanceIdA = null;
            string instanceIdB = null;
            using var hostA = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ =>
                {
                    instanceIdA = service.InstanceId;
                    return new FakeEndpointProvider(typeof(IOrderMessage));
                }))));
            using var hostB = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ =>
                {
                    instanceIdB = service.InstanceId;
                    return new FakeEndpointProvider(typeof(IOrderMessage));
                }))));

            await hostA.StartAsync(TestContext.Current.CancellationToken);
            await hostB.StartAsync(TestContext.Current.CancellationToken);

            Assert.False(string.IsNullOrWhiteSpace(instanceIdA));
            Assert.NotEqual(instanceIdA, instanceIdB);
        }

        [Fact]
        public async Task ShouldUseTheConfiguredInstanceId()
        {
            string instanceId = null;
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .UseInstanceId("orders-worker-1")
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ =>
                {
                    instanceId = service.InstanceId;
                    return new FakeEndpointProvider(typeof(IOrderMessage));
                }))));

            await host.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal("orders-worker-1", instanceId);
        }

        [Fact]
        public void ShouldRejectABlankInstanceId()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentException>(() => services.AddObvs(obvs => obvs.UseInstanceId(" ")));
        }

        [Fact]
        public async Task ShouldGiveTheTransportTheServicesSerializer()
        {
            var serializer = A.Fake<IMessageSerializer>();
            var deserializerFactory = A.Fake<IMessageDeserializerFactory>();
            IMessageSerializer transportSerializer = null;
            IMessageDeserializerFactory transportDeserializerFactory = null;
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service
                    .SerializedWith(serializer, deserializerFactory)
                    .UseEndpointProvider(_ =>
                    {
                        transportSerializer = service.Serializer;
                        transportDeserializerFactory = service.DeserializerFactory;
                        return new FakeEndpointProvider(typeof(IOrderMessage));
                    }))));

            await host.StartAsync(TestContext.Current.CancellationToken);

            Assert.Same(serializer, transportSerializer);
            Assert.Same(deserializerFactory, transportDeserializerFactory);
        }

        [Fact]
        public async Task ShouldReportTransportValidationErrorsWithTheRest()
        {
            IServiceProvider validatedWith = null;
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => { })
                .AddService<IShippingMessage>("shipping", service => service
                    .UseEndpointProvider(_ => new FakeEndpointProvider(typeof(IShippingMessage)))
                    .ValidateWith(serviceProvider =>
                    {
                        validatedWith = serviceProvider;
                        return new[] { "Service 'shipping' needs a broker address." };
                    }))));

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Equal(2, exception.Errors.Count);
            Assert.Contains("Service 'shipping' needs a broker address.", exception.Errors);
            Assert.NotNull(validatedWith);
        }

        [Fact]
        public async Task ShouldPassTheTokenThroughToTheTransport()
        {
            var endpoint = A.Fake<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            var endpointClient = A.Fake<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => endpoint.CanHandle(A<IMessage>._)).Returns(true);
            A.CallTo(() => endpointClient.CanHandle(A<IMessage>._)).Returns(true);
            var provider = A.Fake<IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => provider.CreateEndpoint()).Returns(endpoint);
            A.CallTo(() => provider.CreateEndpointClient()).Returns(endpointClient);
            using var host = BuildHost(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseEndpointProvider(_ => provider))));
            await host.StartAsync(TestContext.Current.CancellationToken);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var command = new PlaceOrder { OrderId = "o-1" };
            var ev = new OrderPlaced { OrderId = "o-1" };
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;

            await bus.SendAsync(command, token);
            await bus.PublishAsync(ev, token);

            A.CallTo(() => endpointClient.SendAsync(command, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => endpoint.PublishAsync(ev, token)).MustHaveHappenedOnceExactly();
        }

        private static IHost BuildHost(Action<IServiceCollection> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            configure(builder.Services);
            return builder.Build();
        }

        public interface IOrderMessage : IMessage { }

        public interface IShippingMessage : IMessage { }

        public class PlaceOrder : IOrderMessage, ICommand
        {
            public string OrderId { get; set; }
        }

        public class OrderPlaced : IOrderMessage, IEvent
        {
            public string OrderId { get; set; }
        }

        // Hands out one shared fake endpoint as both server and client, so messages loop back in-process
        private sealed class FakeEndpointProvider : IServiceEndpointProvider
        {
            public FakeEndpointProvider(Type serviceMessageType)
            {
                Endpoint = new CountingServiceEndpoint(serviceMessageType);
            }

            public CountingServiceEndpoint Endpoint { get; }
            public int EndpointsCreated { get; private set; }
            public int ClientsCreated { get; private set; }

            public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
            {
                EndpointsCreated++;
                return Endpoint;
            }

            public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
            {
                ClientsCreated++;
                return Endpoint;
            }
        }

        private sealed class CountingServiceEndpoint : FakeServiceEndpoint, IDisposable
        {
            public CountingServiceEndpoint(Type serviceType) : base(serviceType)
            {
            }

            public int DisposeCount { get; private set; }

            void IDisposable.Dispose()
            {
                DisposeCount++;
                Dispose();
            }
        }
    }
}
