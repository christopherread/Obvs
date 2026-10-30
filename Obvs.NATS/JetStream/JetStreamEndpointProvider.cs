using System.Collections.Generic;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Transports;
using Obvs.Types;

namespace Obvs.NATS.JetStream
{
    // Generic in the service's message type, which picks the deserializers for the messages the service owns
    internal sealed class JetStreamEndpointProvider<TServiceMessage> : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
        where TServiceMessage : class
    {
        private readonly string _serviceName;
        private readonly string _instanceId;
        private readonly NatsJetStreamOptions _options;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly JetStreamTopology _topology;

        public JetStreamEndpointProvider(string serviceName, string instanceId, NatsJetStreamOptions options,
            IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, bool isServer, bool isClient)
        {
            _serviceName = serviceName;
            _instanceId = instanceId;
            _options = options;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _topology = new JetStreamTopology(serviceName, instanceId, options);

            // A client's own consumers have to exist before anything is published for them
#pragma warning disable VSTHRD002 // Providers are created synchronously, as the bus starts
            var connection = Connect();
            try
            {
                _topology.EnsureCreatedAsync(new NatsJSContext(connection), isServer, isClient).GetAwaiter().GetResult();
            }
            finally
            {
                connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
#pragma warning restore VSTHRD002
        }

        public IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpoint()
        {
            var connection = Connect();
            var jetStream = new NatsJSContext(connection);
            var endpoint = new ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new MessageSource<IRequest>(jetStream, _topology, _topology.Requests, _serviceName, Deserializers<IRequest>(), _options),
                new MessageSource<ICommand>(jetStream, _topology, _topology.Commands, _serviceName, Deserializers<ICommand>(), _options),
                new MessagePublisher<IEvent>(jetStream, _topology.Events.SubjectPrefix, _serializer),
                new DefaultMessagePublisher<IResponse>(), // Responses go to each request's reply subject instead
                typeof(TServiceMessage));
            return new ServiceEndpoint(connection, jetStream, endpoint, _serializer);
        }

        public IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> CreateEndpointClient()
        {
            var connection = Connect();
            var jetStream = new NatsJSContext(connection);
            var endpointClient = new ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>(
                new MessageSource<IEvent>(jetStream, _topology, _topology.Events, _serviceName, Deserializers<IEvent>(), _options),
                new MessageSource<IResponse>(jetStream, _topology, _topology.Responses, _serviceName, Deserializers<IResponse>(), _options),
                new MessagePublisher<IRequest>(jetStream, _topology.Requests.SubjectPrefix, _serializer, _topology.Responses.SubjectPrefix),
                new MessagePublisher<ICommand>(jetStream, _topology.Commands.SubjectPrefix, _serializer),
                typeof(TServiceMessage));
            return new ServiceEndpointClient(connection, jetStream, endpointClient, _topology);
        }

        // Each endpoint has its own connection, so one host stopping can't close another's
        private NatsConnection Connect()
        {
            return JetStreamTopology.Connect(_options, $"{_serviceName}-{_instanceId}");
        }

        private IEnumerable<IMessageDeserializer<T>> Deserializers<T>() where T : class
        {
            return _deserializerFactory.Create<T, TServiceMessage>();
        }
    }
}
