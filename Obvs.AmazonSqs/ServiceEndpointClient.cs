using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.AmazonSqs
{
    // A client of the service: sends commands and requests to the shared queues, and receives events and responses on
    // this bus's own queues, which it keeps recording as seen while it runs, and deletes, with its subscription to the
    // service's topic, when disposed
    internal sealed class ServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IAmazonSQS _sqs;
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _endpointClient;
        private readonly AmazonSqsEntities _entities;
        private readonly BusHousekeeping _housekeeping;
        private int _disposed;

        /// <param name="housekeeping">Null when clean-up is off</param>
        public ServiceEndpointClient(IAmazonSQS sqs, IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> endpointClient, AmazonSqsEntities entities,
            BusHousekeeping housekeeping)
        {
            _sqs = sqs;
            _endpointClient = endpointClient;
            _entities = entities;
            _housekeeping = housekeeping;
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
            // First, so a refresh can't recreate the queues once they're deleted
            _housekeeping?.Dispose();
            _endpointClient.Dispose();
            _sqs.Dispose();
            _entities.DeleteBusEntitiesAsync().GetAwaiter().GetResult();
        }
    }
}
