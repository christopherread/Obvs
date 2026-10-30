using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Obvs.Serialization;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.NATS.JetStream
{
    // The service's side: receives commands and requests through the consumers the service's buses share, publishes
    // events, and sends each response to the subject of the bus that made the request
    internal sealed class ServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly NatsConnection _connection;
        private readonly INatsJSContext _jetStream;
        private readonly IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> _endpoint;
        private readonly IMessageSerializer _serializer;

        public ServiceEndpoint(NatsConnection connection, INatsJSContext jetStream, IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> endpoint, IMessageSerializer serializer)
        {
            _connection = connection;
            _jetStream = jetStream;
            _endpoint = endpoint;
            _serializer = serializer;
        }

        public IObservable<IRequest> Requests => _endpoint.Requests;

        public IObservable<ICommand> Commands => _endpoint.Commands;

        public string Name => _endpoint.Name;

        public bool CanHandle(IMessage message) => _endpoint.CanHandle(message);

        public Task PublishAsync(IEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) => _endpoint.PublishAsync(ev, headers, cancellationToken);

        public Task ReplyAsync(IRequest request, IResponse response, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            if (!ReplyAddresses.TryGet(request, out var replyTo))
            {
                throw new InvalidOperationException(
                    $"Request {request.GetType().Name} {request.RequestId} didn't arrive over NATS JetStream with a reply subject, so there's nowhere to send its {response.GetType().Name}.");
            }

            return MessagePublisher<IResponse>.PublishAsync(_jetStream, replyTo, response, headers, _serializer, cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            _endpoint.Dispose();
#pragma warning disable VSTHRD002 // IDisposable.Dispose is synchronous
            _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
    }
}
