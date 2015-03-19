using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.RabbitMQ.Configuration
{
    public class RabbitMQServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly string _brokerUri;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly string _assemblyNameContains;

        public RabbitMQServiceEndpointProvider(string serviceName, string brokerUri, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, string assemblyNameContains)
            : base(serviceName)
        {
            _brokerUri = brokerUri;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyNameContains = assemblyNameContains;
        }

        public RabbitMQServiceEndpointProvider(string serviceName, string brokerUri, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
            : this(serviceName, brokerUri, serializer, deserializerFactory, string.Empty)
        {
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(
                SourcePublisherFactory.CreateSource<IRequest, TServiceMessage>(_brokerUri, RequestsDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                SourcePublisherFactory.CreateSource<ICommand, TServiceMessage>(_brokerUri, CommandsDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                SourcePublisherFactory.CreatePublisher<IEvent>(_brokerUri, EventsDestination, ServiceName, _serializer),
                SourcePublisherFactory.CreatePublisher<IResponse>(_brokerUri, ResponsesDestination, ServiceName, _serializer),
                typeof(TServiceMessage));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(
                SourcePublisherFactory.CreateSource<IEvent, TServiceMessage>(_brokerUri, EventsDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                SourcePublisherFactory.CreateSource<IResponse, TServiceMessage>(_brokerUri, ResponsesDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                SourcePublisherFactory.CreatePublisher<IRequest>(_brokerUri, RequestsDestination, ServiceName, _serializer),
                SourcePublisherFactory.CreatePublisher<ICommand>(_brokerUri, CommandsDestination, ServiceName, _serializer),
                typeof(TServiceMessage));
        }
    }
}