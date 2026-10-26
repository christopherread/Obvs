using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.AzureSB.Tests.Hosting
{
    // What happens when Obvs can't set up a service's entities: on a Basic-tier namespace, which has no topics,
    // and with credentials that can send and listen but lack the Manage right. The emulator can't refuse either, so
    // a stand-in for the management client gives the answers Azure documents.
    public class TestAzureServiceBusEntitySetup : IClassFixture<TestAzureServiceBusHosting.ServiceNames>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly TestAzureServiceBusHosting.ServiceNames _serviceNames;

        public TestAzureServiceBusEntitySetup(TestAzureServiceBusHosting.ServiceNames serviceNames)
        {
            _serviceNames = serviceNames;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldWorkWithoutTheManageRightWhenTheEntitiesAlreadyExist()
        {
            var serviceName = _serviceNames.Create("no-manage");
            const string instanceId = "orders-worker-1";
            await CreateEntitiesAsync(serviceName, instanceId, commands: true, requests: true, events: true, subscription: true, replies: true);
            using var host = EntitySetup.BuildHost(serviceName, new EntitySetup.UnauthorizedAdministration(), options => options.ConnectionString = TestBroker.ConnectionString, instanceId);
            await host.StartAsync(Ct);
            var bus = host.Services.GetRequiredService<IServiceBus>();

            var command = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();
            var placed = bus.Events.OfType<OrderPlaced>().FirstAsync().ToTask();
            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });
            await bus.PublishAsync(new OrderPlaced { OrderId = "o-1" });

            Assert.Equal("o-1", (await command.WaitAsync(Timeout, Ct)).OrderId);
            Assert.Equal("o-1", (await placed.WaitAsync(Timeout, Ct)).OrderId);
            await host.StopAsync(Ct);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReportWhichEntitiesAreMissingWithoutTheManageRight()
        {
            var serviceName = _serviceNames.Create("no-manage-missing");
            const string instanceId = "orders-worker-1";
            await CreateEntitiesAsync(serviceName, instanceId, commands: true, requests: false, events: true, subscription: false, replies: false);
            using var host = EntitySetup.BuildHost(serviceName, new EntitySetup.UnauthorizedAdministration(), options => options.ConnectionString = TestBroker.ConnectionString, instanceId);

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(Ct));

            var error = Assert.Single(exception.Errors);
            Assert.Contains($"'{serviceName}'", error);
            Assert.Contains("Manage", error);
            Assert.Contains($"queue {serviceName}.Requests", error);
            Assert.Contains($"subscription {instanceId} on topic {serviceName}.Events", error);
            Assert.Contains($"queue {serviceName}.Responses.{instanceId}", error);
            Assert.DoesNotContain($"queue {serviceName}.Commands", error);
            Assert.Contains("UseInstanceId", error);
        }

        private static async Task CreateEntitiesAsync(string serviceName, string instanceId, bool commands, bool requests, bool events, bool subscription, bool replies)
        {
            var admin = TestBroker.CreateAdministrationClient();
            if (commands)
            {
                await admin.CreateQueueAsync($"{serviceName}.Commands", Ct);
            }
            if (requests)
            {
                await admin.CreateQueueAsync($"{serviceName}.Requests", Ct);
            }
            if (events)
            {
                await admin.CreateTopicAsync($"{serviceName}.Events", Ct);
            }
            if (subscription)
            {
                await admin.CreateSubscriptionAsync($"{serviceName}.Events", instanceId, Ct);
            }
            if (replies)
            {
                await admin.CreateQueueAsync($"{serviceName}.Responses.{instanceId}", Ct);
            }
        }

    }

    // Needs no broker: the stand-in refuses the topic as a Basic-tier namespace does, before anything connects
    public class TestAzureServiceBusBasicTier
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public static TheoryData<string> BasicTierRefusals => new TheoryData<string> { nameof(ServiceBusException), nameof(RequestFailedException) };

        [Theory]
        [MemberData(nameof(BasicTierRefusals))]
        public async Task ShouldFailToStartOnABasicTierNamespace(string refusal)
        {
            const string basicTier = "SubCode=40000. Cannot operate on type Topic because the namespace 'contoso' is using 'Basic' tier.";
            var admin = new EntitySetup.InMemoryAdministration
            {
                RefuseTopics = refusal == nameof(ServiceBusException)
                    ? new ServiceBusException(basicTier, ServiceBusFailureReason.GeneralError)
                    : new RequestFailedException(400, basicTier),
            };
            using var host = EntitySetup.BuildHost("orders", admin, options => options.FullyQualifiedNamespace = "contoso.servicebus.windows.net");

            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(Ct));

            var error = Assert.Single(exception.Errors);
            Assert.Contains("'orders'", error);
            Assert.Contains("Basic tier", error);
            Assert.Contains("Standard or Premium", error);
        }
    }

    // Hosts whose transport uses a stand-in for Service Bus's management client
    internal static class EntitySetup
    {
        public static IHost BuildHost(string serviceName, IServiceBusAdministration admin, Action<AzureServiceBusOptions> configure, string instanceId = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddSingleton<IServiceBusAdministrationFactory>(new AdministrationFactory(admin));
            builder.Services.AddObvs(obvs =>
            {
                if (instanceId != null)
                {
                    obvs.UseInstanceId(instanceId);
                }
                obvs.AddService<IOrderMessage>(serviceName, service => service
                    .UseAzureServiceBus(configure)
                    .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))));
            });
            return builder.Build();
        }

        internal sealed class AdministrationFactory : IServiceBusAdministrationFactory
        {
            private readonly IServiceBusAdministration _admin;

            public AdministrationFactory(IServiceBusAdministration admin) => _admin = admin;

            public IServiceBusAdministration Create(AzureServiceBusOptions options) => _admin;
        }

        // Keeps entities in memory, and can refuse to create topics as a Basic-tier namespace does
        internal sealed class InMemoryAdministration : IServiceBusAdministration
        {
            private readonly HashSet<string> _entities = new HashSet<string>();

            public Exception RefuseTopics { get; set; }

            public Task<bool> QueueExistsAsync(string queue) => Task.FromResult(_entities.Contains(queue));

            public Task CreateQueueAsync(CreateQueueOptions queue) => Add(queue.Name);

            public Task DeleteQueueAsync(string queue) => Remove(queue);

            public Task<bool> TopicExistsAsync(string topic) => Task.FromResult(_entities.Contains(topic));

            public Task CreateTopicAsync(string topic) => RefuseTopics != null ? Task.FromException(RefuseTopics) : Add(topic);

            public Task<bool> SubscriptionExistsAsync(string topic, string subscription) => Task.FromResult(_entities.Contains($"{topic}/{subscription}"));

            public Task CreateSubscriptionAsync(CreateSubscriptionOptions subscription) => Add($"{subscription.TopicName}/{subscription.SubscriptionName}");

            public Task DeleteSubscriptionAsync(string topic, string subscription) => Remove($"{topic}/{subscription}");

            private Task Add(string entity)
            {
                _entities.Add(entity);
                return Task.CompletedTask;
            }

            private Task Remove(string entity)
            {
                _entities.Remove(entity);
                return Task.CompletedTask;
            }
        }

        // Every management operation fails as it does for credentials without the Manage right
        internal sealed class UnauthorizedAdministration : IServiceBusAdministration
        {
            private static Task<T> Refuse<T>() => Task.FromException<T>(
                new UnauthorizedAccessException("Unauthorized. The token is missing the 'Manage' claim required for this operation."));

            public Task<bool> QueueExistsAsync(string queue) => Refuse<bool>();

            public Task CreateQueueAsync(CreateQueueOptions queue) => Refuse<bool>();

            public Task DeleteQueueAsync(string queue) => Refuse<bool>();

            public Task<bool> TopicExistsAsync(string topic) => Refuse<bool>();

            public Task CreateTopicAsync(string topic) => Refuse<bool>();

            public Task<bool> SubscriptionExistsAsync(string topic, string subscription) => Refuse<bool>();

            public Task CreateSubscriptionAsync(CreateSubscriptionOptions subscription) => Refuse<bool>();

            public Task DeleteSubscriptionAsync(string topic, string subscription) => Refuse<bool>();
        }
    }
}
