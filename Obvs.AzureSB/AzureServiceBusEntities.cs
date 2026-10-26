using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Obvs.AzureSB
{
    // A service's entities, and this bus's own: the subscription it receives events on and the queue it receives replies on
    internal sealed class AzureServiceBusEntities
    {
        // A bus's own entities delete themselves after this long without use, so a crashed bus doesn't leave them behind.
        // It's the shortest Service Bus allows.
        private static readonly TimeSpan AutoDeleteOnIdle = TimeSpan.FromMinutes(5);

        private readonly ServiceBusAdministrationClient _admin;
        private readonly int _maxDeliveryCount;

        public AzureServiceBusEntities(string serviceName, string instanceId, AzureServiceBusOptions options)
        {
            _maxDeliveryCount = options.MaxDeliveryAttempts;
            Commands = $"{serviceName}.Commands";
            Requests = $"{serviceName}.Requests";
            Events = $"{serviceName}.Events";
            EventSubscription = instanceId;
            Replies = $"{serviceName}.Responses.{instanceId}";
            _admin = CreateAdministrationClient(options);
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

        // Entities are created on first use by any bus of the service; another bus may create them at the same time
        public async Task CreateServiceEntitiesAsync()
        {
            await CreateQueueAsync(new CreateQueueOptions(Commands) { MaxDeliveryCount = _maxDeliveryCount }).ConfigureAwait(false);
            await CreateQueueAsync(new CreateQueueOptions(Requests) { MaxDeliveryCount = _maxDeliveryCount }).ConfigureAwait(false);
            await IgnoreIfExists(async () =>
            {
                if (!(await _admin.TopicExistsAsync(Events).ConfigureAwait(false)).Value)
                {
                    await _admin.CreateTopicAsync(Events).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        public async Task CreateBusEntitiesAsync()
        {
            await IgnoreIfExists(async () =>
            {
                if (!(await _admin.SubscriptionExistsAsync(Events, EventSubscription).ConfigureAwait(false)).Value)
                {
                    await _admin.CreateSubscriptionAsync(new CreateSubscriptionOptions(Events, EventSubscription) { AutoDeleteOnIdle = AutoDeleteOnIdle, MaxDeliveryCount = _maxDeliveryCount }).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
            await CreateQueueAsync(new CreateQueueOptions(Replies) { AutoDeleteOnIdle = AutoDeleteOnIdle, MaxDeliveryCount = _maxDeliveryCount }).ConfigureAwait(false);
        }

        public async Task DeleteBusEntitiesAsync()
        {
            await IgnoreIfMissing(() => _admin.DeleteSubscriptionAsync(Events, EventSubscription)).ConfigureAwait(false);
            await IgnoreIfMissing(() => _admin.DeleteQueueAsync(Replies)).ConfigureAwait(false);
        }

        private Task CreateQueueAsync(CreateQueueOptions queue)
        {
            return IgnoreIfExists(async () =>
            {
                if (!(await _admin.QueueExistsAsync(queue.Name).ConfigureAwait(false)).Value)
                {
                    await _admin.CreateQueueAsync(queue).ConfigureAwait(false);
                }
            });
        }

        private static async Task IgnoreIfExists(Func<Task> create)
        {
            try
            {
                await create().ConfigureAwait(false);
            }
            catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
        }

        private static async Task IgnoreIfMissing(Func<Task<Response>> delete)
        {
            try
            {
                await delete().ConfigureAwait(false);
            }
            catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
            }
        }

        // The emulator serves management operations on port 5300, whichever port its connection string gives for messaging
        private static ServiceBusAdministrationClient CreateAdministrationClient(AzureServiceBusOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace))
            {
                return new ServiceBusAdministrationClient(options.FullyQualifiedNamespace, options.Credential ?? new DefaultAzureCredential());
            }

            var connectionString = options.ConnectionString;
            if (connectionString.IndexOf("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                connectionString = Regex.Replace(connectionString, @"Endpoint=sb://([^;:/]+)(:\d+)?/?;", "Endpoint=sb://$1:5300;", RegexOptions.IgnoreCase);
            }
            return new ServiceBusAdministrationClient(connectionString);
        }
    }
}
