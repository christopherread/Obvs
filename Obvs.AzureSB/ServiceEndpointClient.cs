using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.AzureSB
{
    // A client of the service: sends commands and requests to the shared queues, and receives events on this bus's
    // own subscription and responses on its own reply queue, both of which it deletes when disposed
    internal sealed class ServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly Azure.Messaging.ServiceBus.ServiceBusClient _client;
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _endpointClient;
        private readonly AzureServiceBusEntities _entities;
        private int _disposed;

        public ServiceEndpointClient(Azure.Messaging.ServiceBus.ServiceBusClient client, IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> endpointClient, AzureServiceBusEntities entities)
        {
            _client = client;
            _endpointClient = endpointClient;
            _entities = entities;
        }

        public IObservable<IEvent> Events => _endpointClient.Events;

        public string Name => _endpointClient.Name;

        public bool CanHandle(IMessage message) => _endpointClient.CanHandle(message);

        public Task SendAsync(ICommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) => _endpointClient.SendAsync(command, headers, cancellationToken);

        public IObservable<IResponse> GetResponses(IRequest request, IReadOnlyDictionary<string, string> headers) => _endpointClient.GetResponses(request, headers);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            _endpointClient.Dispose();
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _entities.DeleteBusEntitiesAsync().GetAwaiter().GetResult();
        }
    }
}
