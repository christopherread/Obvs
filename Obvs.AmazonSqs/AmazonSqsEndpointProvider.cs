using System.Collections.Generic;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.AmazonSqs
{
    // Generic in the service's message type, which picks the deserializers for the messages the service owns
    internal sealed class AmazonSqsEndpointProvider<TServiceMessage> : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
        where TServiceMessage : class
    {
        private readonly AmazonSqsOptions _options;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly AmazonSqsEntities _entities;

        public AmazonSqsEndpointProvider(string serviceName, string instanceId, AmazonSqsOptions options,
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, bool isClient)
        {
            _options = options;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _entities = new AmazonSqsEntities(serviceName, instanceId, options);
            // A client also needs this bus's own queues and subscription, ready before anything is published to them
            _entities.EnsureCreatedAsync(isClient).GetAwaiter().GetResult();
        }

        public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
        {
            var sqs = AmazonSqsEntities.CreateSqsClient(_options);
            var sns = AmazonSqsEntities.CreateSnsClient(_options);
            var endpoint = new ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new MessageSource<IRequest>(sqs, _entities.RequestsUrl, _entities.DeadLettersUrl, Deserializers<IRequest>(), _options),
                new MessageSource<ICommand>(sqs, _entities.CommandsUrl, _entities.DeadLettersUrl, Deserializers<ICommand>(), _options),
                new TopicMessagePublisher<IEvent>(sns, _entities.EventsTopicArn, _serializer),
                new DefaultMessagePublisher<IResponse>(), // Responses go to each request's reply queue instead
                typeof(TServiceMessage));
            return new ServiceEndpoint(sqs, sns, endpoint, _serializer);
        }

        public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
        {
            var sqs = AmazonSqsEntities.CreateSqsClient(_options);
            // With clean-up on, the bus recreates its own queues if another bus deletes them, so receiving waits for them
            var recreated = _options.CleanUpAbandonedQueues;
            var endpointClient = new ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new MessageSource<IEvent>(sqs, _entities.EventQueueUrl, _entities.DeadLettersUrl, Deserializers<IEvent>(), _options, recreated),
                new MessageSource<IResponse>(sqs, _entities.RepliesUrl, _entities.DeadLettersUrl, Deserializers<IResponse>(), _options, recreated),
                new QueueMessagePublisher<IRequest>(sqs, _entities.RequestsUrl, _serializer, _entities.RepliesUrl),
                new QueueMessagePublisher<ICommand>(sqs, _entities.CommandsUrl, _serializer),
                typeof(TServiceMessage));
            var housekeeping = _options.CleanUpAbandonedQueues ? new BusHousekeeping(_entities, _options.LastSeenInterval) : null;
            housekeeping?.Start();
            return new ServiceEndpointClient(sqs, endpointClient, _entities, housekeeping);
        }

        private IEnumerable<IMessageDeserializer<T>> Deserializers<T>() where T : class
        {
            return _deserializerFactory.Create<T, TServiceMessage>();
        }
    }
}
