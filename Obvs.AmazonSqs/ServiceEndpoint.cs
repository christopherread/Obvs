using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.AmazonSqs
{
    // The service's side: receives commands and requests from the shared queues, publishes events to the topic,
    // and sends each response to the reply queue of the bus that made the request
    internal sealed class ServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        private readonly IAmazonSQS _sqs;
        private readonly IAmazonSimpleNotificationService _sns;
        private readonly IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> _endpoint;
        private readonly IMessageSerializer _serializer;

        public ServiceEndpoint(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> endpoint, IMessageSerializer serializer)
        {
            _sqs = sqs;
            _sns = sns;
            _endpoint = endpoint;
            _serializer = serializer;
        }

        public IObservable<IRequest> Requests => _endpoint.Requests;

        public IObservable<ICommand> Commands => _endpoint.Commands;

        public string Name => _endpoint.Name;

        public bool CanHandle(IMessage message) => _endpoint.CanHandle(message);

        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default) => _endpoint.PublishAsync(ev, cancellationToken);

        public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default)
        {
            if (!ReplyAddresses.TryGet(request, out var replyTo))
            {
                throw new InvalidOperationException(
                    $"Request {request.GetType().Name} {request.RequestId} didn't arrive over Amazon SQS with a reply queue, so there's nowhere to send its {response.GetType().Name}.");
            }

            return QueueMessagePublisher<IResponse>.SendAsync(_sqs, replyTo, response, _serializer, cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            _endpoint.Dispose();
            _sqs.Dispose();
            _sns.Dispose();
        }
    }
}
