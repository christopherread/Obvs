using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Obvs.Serialization;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.AzureSB
{
    // The service's side: receives commands and requests from the shared queues, publishes events to the topic,
    // and sends each response to the reply queue of the bus that made the request
    internal sealed class ServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly Azure.Messaging.ServiceBus.ServiceBusClient _client;
        private readonly IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> _endpoint;
        private readonly IMessageSerializer _serializer;
        private readonly ConcurrentDictionary<string, ServiceBusSender> _replySenders = new ConcurrentDictionary<string, ServiceBusSender>();

        public ServiceEndpoint(Azure.Messaging.ServiceBus.ServiceBusClient client, IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> endpoint, IMessageSerializer serializer)
        {
            _client = client;
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
                    $"Request {request.GetType().Name} {request.RequestId} didn't arrive over Azure Service Bus with a reply queue, so there's nowhere to send its {response.GetType().Name}.");
            }

            var sender = _replySenders.GetOrAdd(replyTo, queue => _client.CreateSender(queue));
            return sender.SendMessageAsync(MessagePublisher<IResponse>.ToServiceBusMessage(response, headers, _serializer), cancellationToken);
        }

        public void Dispose()
        {
            _endpoint.Dispose();
            foreach (var sender in _replySenders.Values)
            {
                sender.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
