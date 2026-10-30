using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.NATS.JetStream
{
    // A client of the service: sends commands and requests to the service's streams, and receives events and responses
    // through this bus's own consumers, which it deletes when disposed
    internal sealed class ServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        // How long stopping waits to delete the bus's own consumers, which delete themselves once idle anyway
        private static readonly TimeSpan DeleteTimeout = TimeSpan.FromSeconds(2);

        private readonly NatsConnection _connection;
        private readonly INatsJSContext _jetStream;
        private readonly IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> _endpointClient;
        private readonly JetStreamTopology _topology;
        private int _disposed;

        public ServiceEndpointClient(NatsConnection connection, INatsJSContext jetStream, IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> endpointClient,
            JetStreamTopology topology)
        {
            _connection = connection;
            _jetStream = jetStream;
            _endpointClient = endpointClient;
            _topology = topology;
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
#pragma warning disable VSTHRD002 // IDisposable.Dispose is synchronous
            using (var deleting = new CancellationTokenSource(DeleteTimeout))
            {
                _topology.DeleteBusConsumersAsync(_jetStream, deleting.Token).GetAwaiter().GetResult();
            }
            _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
    }
}
