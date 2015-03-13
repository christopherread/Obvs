using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Configuration
{
    public class ActiveMQServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly string _brokerUri;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly string _assemblyNameContains;

        public ActiveMQServiceEndpointProvider(string serviceName, string brokerUri, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, string assemblyNameContains)
            : base(serviceName)
        {
            _brokerUri = brokerUri;
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyNameContains = assemblyNameContains;
        }

        public ActiveMQServiceEndpointProvider(string serviceName, string brokerUri, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory)
            : this(serviceName, brokerUri, serializer, deserializerFactory, string.Empty)
        {
            _brokerUri = brokerUri;
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(
                DestinationFactory.CreateSource<IRequest, TServiceMessage>(_brokerUri, RequestsDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreateSource<ICommand, TServiceMessage>(_brokerUri, CommandsDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreatePublisher<IEvent>(_brokerUri, EventsDestination, ServiceName, _serializer),
                DestinationFactory.CreatePublisher<IResponse>(_brokerUri, ResponsesDestination, ServiceName, _serializer),
                typeof(TServiceMessage));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(
                DestinationFactory.CreateSource<IEvent, TServiceMessage>(_brokerUri, EventsDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreateSource<IResponse, TServiceMessage>(_brokerUri, ResponsesDestination, ServiceName, _deserializerFactory, _assemblyNameContains),
                DestinationFactory.CreatePublisher<IRequest>(_brokerUri, RequestsDestination, ServiceName, _serializer),
                DestinationFactory.CreatePublisher<ICommand>(_brokerUri, CommandsDestination, ServiceName, _serializer),
                typeof(TServiceMessage));
        }
    }
}