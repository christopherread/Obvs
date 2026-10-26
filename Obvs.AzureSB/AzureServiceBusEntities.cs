using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Obvs.Hosting;

namespace Obvs.AzureSB
{
    // A service's entities, and this bus's own: the subscription it receives events on and the queue it receives replies on
    internal sealed class AzureServiceBusEntities
    {
        // A bus's own entities delete themselves after this long without use, so a crashed bus doesn't leave them behind.
        // It's the shortest Service Bus allows.
        private static readonly TimeSpan AutoDeleteOnIdle = TimeSpan.FromMinutes(5);

        private readonly string _serviceName;
        private readonly AzureServiceBusOptions _options;
        private readonly IServiceBusAdministration _admin;
        private bool _cantManage;

        public AzureServiceBusEntities(string serviceName, string instanceId, AzureServiceBusOptions options, IServiceBusAdministration admin)
        {
            _serviceName = serviceName;
            _options = options;
            _admin = admin;
            Commands = $"{serviceName}.Commands";
            Requests = $"{serviceName}.Requests";
            Events = $"{serviceName}.Events";
            EventSubscription = instanceId;
            Replies = $"{serviceName}.Responses.{instanceId}";
        }

        public string Commands { get; }
        public string Requests { get; }
        public string Events { get; }
        public string EventSubscription { get; }
        public string Replies { get; }

        public static Azure.Messaging.ServiceBus.ServiceBusClient CreateClient(AzureServiceBusOptions options)
        {
            return string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new Azure.Messaging.ServiceBus.ServiceBusClient(options.FullyQualifiedNamespace, options.Credential ?? new DefaultAzureCredential())
                : new Azure.Messaging.ServiceBus.ServiceBusClient(options.ConnectionString);
        }

        // Creates the entities the bus needs that don't exist yet; another bus of the service may create them at the same
        // time. Credentials without the Manage right can't, so then each is checked through messaging instead, and the
        // bus fails to start if any is missing.
        public async Task EnsureCreatedAsync(bool includeBusEntities)
        {
            var entities = ServiceEntities().Concat(includeBusEntities ? BusEntities() : Enumerable.Empty<Entity>()).ToList();
            foreach (var entity in entities)
            {
                try
                {
                    if (!await entity.ExistsAsync(_admin).ConfigureAwait(false))
                    {
                        await entity.CreateAsync(_admin).ConfigureAwait(false);
                    }
                }
                catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                }
                catch (UnauthorizedAccessException)
                {
                    _cantManage = true;
                    break;
                }
                catch (Exception exception) when (entity.IsTopic && IsBasicTier(exception))
                {
                    throw new ObvsConfigurationException(new[]
                    {
                        $"Service '{_serviceName}' publishes its events to an Azure Service Bus topic, but the namespace is on the Basic tier, " +
                        "which has no topics. Use a Standard or Premium namespace.",
                    });
                }
            }

            if (_cantManage)
            {
                await ReportMissingAsync(entities).ConfigureAwait(false);
            }
        }

        // Entities it couldn't create are left for whoever created them
        public async Task DeleteBusEntitiesAsync()
        {
            if (_cantManage)
            {
                return;
            }

            try
            {
                await IgnoreIfMissing(() => _admin.DeleteSubscriptionAsync(Events, EventSubscription)).ConfigureAwait(false);
                await IgnoreIfMissing(() => _admin.DeleteQueueAsync(Replies)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private IEnumerable<Entity> ServiceEntities()
        {
            yield return Queue(Commands, new CreateQueueOptions(Commands));
            yield return Queue(Requests, new CreateQueueOptions(Requests));
            yield return new Entity(
                $"topic {Events}",
                admin => admin.TopicExistsAsync(Events),
                admin => admin.CreateTopicAsync(Events),
                client => ProbeTopicAsync(client, Events),
                isTopic: true);
        }

        private IEnumerable<Entity> BusEntities()
        {
            var subscription = new CreateSubscriptionOptions(Events, EventSubscription) { AutoDeleteOnIdle = AutoDeleteOnIdle, MaxDeliveryCount = _options.MaxDeliveryAttempts };
            if (_options.LockDuration is TimeSpan lockDuration)
            {
                subscription.LockDuration = lockDuration;
            }
            yield return new Entity(
                $"subscription {EventSubscription} on topic {Events}",
                admin => admin.SubscriptionExistsAsync(Events, EventSubscription),
                admin => admin.CreateSubscriptionAsync(subscription),
                client => ProbeReceiverAsync(client.CreateReceiver(Events, EventSubscription)));
            yield return Queue(Replies, new CreateQueueOptions(Replies) { AutoDeleteOnIdle = AutoDeleteOnIdle });
        }

        private Entity Queue(string name, CreateQueueOptions queue)
        {
            queue.MaxDeliveryCount = _options.MaxDeliveryAttempts;
            if (_options.LockDuration is TimeSpan lockDuration)
            {
                queue.LockDuration = lockDuration;
            }
            return new Entity(
                $"queue {name}",
                admin => admin.QueueExistsAsync(name),
                admin => admin.CreateQueueAsync(queue),
                client => ProbeReceiverAsync(client.CreateReceiver(name)));
        }

        private async Task ReportMissingAsync(IReadOnlyList<Entity> entities)
        {
            var client = CreateClient(_options);
            try
            {
                var missing = new List<string>();
                foreach (var entity in entities)
                {
                    try
                    {
                        await entity.ProbeAsync(client).ConfigureAwait(false);
                    }
                    // A missing entity is reported as not found, except that the emulator never answers for a missing
                    // subscription on a topic that exists. Either way the bus can't confirm the entity is there.
                    catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound ||
                                                                exception.Reason == ServiceBusFailureReason.ServiceTimeout)
                    {
                        missing.Add(entity.Description);
                    }
                }

                if (missing.Count > 0)
                {
                    throw new ObvsConfigurationException(new[]
                    {
                        $"Service '{_serviceName}' can't create its Azure Service Bus entities, as its credentials don't have the Manage right, " +
                        $"and these couldn't be found: {string.Join(", ", missing)}. Create them beforehand, or use credentials with the Manage right. " +
                        "This bus's own subscription and reply queue are named after its instance ID, so give it a fixed one with UseInstanceId(...) to create them beforehand.",
                    });
                }
            }
            finally
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }

        // Peeking needs only the Listen right, and fails if the queue or subscription doesn't exist
        private static async Task ProbeReceiverAsync(ServiceBusReceiver receiver)
        {
            try
            {
                await receiver.PeekMessageAsync().ConfigureAwait(false);
            }
            finally
            {
                await receiver.DisposeAsync().ConfigureAwait(false);
            }
        }

        // Preparing a batch opens a link to the topic, which needs only the Send right, without sending anything
        private static async Task ProbeTopicAsync(Azure.Messaging.ServiceBus.ServiceBusClient client, string topic)
        {
            var sender = client.CreateSender(topic);
            try
            {
                using (await sender.CreateMessageBatchAsync().ConfigureAwait(false))
                {
                }
            }
            finally
            {
                await sender.DisposeAsync().ConfigureAwait(false);
            }
        }

        // A Basic-tier namespace refuses topics with an error naming its tier
        private static bool IsBasicTier(Exception exception)
        {
            return exception.Message.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   exception.Message.IndexOf("tier", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task IgnoreIfMissing(Func<Task> delete)
        {
            try
            {
                await delete().ConfigureAwait(false);
            }
            catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        private sealed class Entity
        {
            private readonly Func<IServiceBusAdministration, Task<bool>> _exists;
            private readonly Func<IServiceBusAdministration, Task> _create;
            private readonly Func<Azure.Messaging.ServiceBus.ServiceBusClient, Task> _probe;

            public Entity(string description, Func<IServiceBusAdministration, Task<bool>> exists, Func<IServiceBusAdministration, Task> create,
                Func<Azure.Messaging.ServiceBus.ServiceBusClient, Task> probe, bool isTopic = false)
            {
                Description = description;
                _exists = exists;
                _create = create;
                _probe = probe;
                IsTopic = isTopic;
            }

            public string Description { get; }
            public bool IsTopic { get; }

            public Task<bool> ExistsAsync(IServiceBusAdministration admin) => _exists(admin);
            public Task CreateAsync(IServiceBusAdministration admin) => _create(admin);
            public Task ProbeAsync(Azure.Messaging.ServiceBus.ServiceBusClient client) => _probe(client);
        }
    }
}
