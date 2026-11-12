using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;

namespace Obvs.AzureSB
{
    // The management operations the transport uses, so tests can stand in for answers the emulator can't give,
    // such as a Basic-tier namespace or credentials without the Manage right
    internal interface IServiceBusAdministration
    {
        Task<bool> QueueExistsAsync(string queue);
        Task CreateQueueAsync(CreateQueueOptions queue);
        Task DeleteQueueAsync(string queue);
        Task<bool> TopicExistsAsync(string topic);
        Task CreateTopicAsync(string topic);
        Task<bool> SubscriptionExistsAsync(string topic, string subscription);
        Task CreateSubscriptionAsync(CreateSubscriptionOptions subscription);
        Task DeleteSubscriptionAsync(string topic, string subscription);
    }

    // Registered in DI only by tests; otherwise the transport uses ServiceBusAdministration
    internal interface IServiceBusAdministrationFactory
    {
        IServiceBusAdministration Create(AzureServiceBusOptions options);
    }

    internal sealed class ServiceBusAdministration : IServiceBusAdministration
    {
        private readonly ServiceBusAdministrationClient _client;

        public ServiceBusAdministration(AzureServiceBusOptions options)
        {
            _client = CreateClient(options);
        }

        public async Task<bool> QueueExistsAsync(string queue) => (await _client.QueueExistsAsync(queue).ConfigureAwait(false)).Value;

        public Task CreateQueueAsync(CreateQueueOptions queue) => _client.CreateQueueAsync(queue);

        public Task DeleteQueueAsync(string queue) => _client.DeleteQueueAsync(queue);

        public async Task<bool> TopicExistsAsync(string topic) => (await _client.TopicExistsAsync(topic).ConfigureAwait(false)).Value;

        public Task CreateTopicAsync(string topic) => _client.CreateTopicAsync(topic);

        public async Task<bool> SubscriptionExistsAsync(string topic, string subscription) =>
            (await _client.SubscriptionExistsAsync(topic, subscription).ConfigureAwait(false)).Value;

        public Task CreateSubscriptionAsync(CreateSubscriptionOptions subscription) => _client.CreateSubscriptionAsync(subscription);

        public Task DeleteSubscriptionAsync(string topic, string subscription) => _client.DeleteSubscriptionAsync(topic, subscription);

        // The emulator serves management operations on port 5300, whichever port its connection string gives for messaging
        private static ServiceBusAdministrationClient CreateClient(AzureServiceBusOptions options)
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
