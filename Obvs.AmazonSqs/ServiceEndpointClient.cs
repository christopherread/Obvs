using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Obvs.Types;

namespace Obvs.AmazonSqs
{
    // A client of the service: sends commands and requests to the shared queues, and receives events and responses on
    // this bus's own queues, which it deletes, with its subscription to the service's topic, when disposed
    internal sealed class ServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IAmazonSQS _sqs;
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _endpointClient;
        private readonly AmazonSqsEntities _entities;
        private int _disposed;

        public ServiceEndpointClient(IAmazonSQS sqs, IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> endpointClient, AmazonSqsEntities entities)
        {
            _sqs = sqs;
            _endpointClient = endpointClient;
            _entities = entities;
        }

        public IObservable<IEvent> Events => _endpointClient.Events;

        public string Name => _endpointClient.Name;

        public bool CanHandle(IMessage message) => _endpointClient.CanHandle(message);

        public Task SendAsync(ICommand command) => _endpointClient.SendAsync(command);

        public IObservable<IResponse> GetResponses(IRequest request) => _endpointClient.GetResponses(request);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            _endpointClient.Dispose();
            _sqs.Dispose();
            _entities.DeleteBusEntitiesAsync().GetAwaiter().GetResult();
        }
    }
}
