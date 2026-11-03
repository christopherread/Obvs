using System;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.AzureSB
{
    // Generic in the service's message type, which picks the deserializers for the messages the service owns
    internal sealed class AzureServiceBusEndpointProvider<TServiceMessage> : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
        where TServiceMessage : class
    {
        private readonly string _serviceName;
        private readonly AzureServiceBusOptions _options;
        private readonly Func<Type, int> _concurrency;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly AzureServiceBusEntities _entities;

        /// <param name="concurrency">How many messages of each kind to take at once</param>
        public AzureServiceBusEndpointProvider(string serviceName, string instanceId, AzureServiceBusOptions options, Func<Type, int> concurrency,
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, IServiceBusAdministration admin, bool isClient)
        {
            _serviceName = serviceName;
            _options = options;
            _concurrency = concurrency;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _entities = new AzureServiceBusEntities(serviceName, instanceId, options, admin);
            // A client also needs this bus's own subscription and reply queue, ready before anything is published to them
            _entities.EnsureCreatedAsync(isClient).GetAwaiter().GetResult();
        }

        public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
        {
            var client = AzureServiceBusEntities.CreateClient(_options);
            var endpoint = new ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new MessageSource<IRequest>(options => client.CreateProcessor(_entities.Requests, options), Deserializers<IRequest>(), _options, _concurrency(typeof(IRequest))),
                new MessageSource<ICommand>(options => client.CreateProcessor(_entities.Commands, options), Deserializers<ICommand>(), _options, _concurrency(typeof(ICommand))),
                new MessagePublisher<IEvent>(client.CreateSender(_entities.Events), _serializer),
                new DefaultMessagePublisher<IResponse>(), // Responses go to each request's reply queue instead
                typeof(TServiceMessage));
            return new ServiceEndpoint(client, endpoint, _serializer);
        }

        public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
        {
            var client = AzureServiceBusEntities.CreateClient(_options);
            var endpointClient = new ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new MessageSource<IEvent>(options => client.CreateProcessor(_entities.Events, _entities.EventSubscription, options), Deserializers<IEvent>(), _options, _concurrency(typeof(IEvent))),
                new MessageSource<IResponse>(options => client.CreateProcessor(_entities.Replies, options), Deserializers<IResponse>(), _options, _concurrency(typeof(IResponse))),
                new MessagePublisher<IRequest>(client.CreateSender(_entities.Requests), _serializer, _entities.Replies),
                new MessagePublisher<ICommand>(client.CreateSender(_entities.Commands), _serializer),
                typeof(TServiceMessage));
            return new ServiceEndpointClient(client, endpointClient, _entities);
        }

        private System.Collections.Generic.IEnumerable<IMessageDeserializer<T>> Deserializers<T>() where T : class
        {
            return _deserializerFactory.Create<T, TServiceMessage>();
        }
    }
}
