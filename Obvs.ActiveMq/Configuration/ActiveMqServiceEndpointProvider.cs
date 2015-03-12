using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMq.Configuration
{
    public class ActiveMqServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly string _brokerUri;
        private readonly string _assemblyNameContains;

        public ActiveMqServiceEndpointProvider(string serviceName, string brokerUri, string assemblyNameContains)
            : base(serviceName)
        {
            _brokerUri = brokerUri;
            _assemblyNameContains = assemblyNameContains;
        }
        
        public ActiveMqServiceEndpointProvider(string serviceName, string brokerUri) : this(serviceName, brokerUri, string.Empty)
        {
            _brokerUri = brokerUri;
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(
                DestinationFactory.CreateSource<IRequest, TServiceMessage>(_brokerUri, RequestsDestination, ServiceName, _assemblyNameContains),
                DestinationFactory.CreateSource<ICommand, TServiceMessage>(_brokerUri, CommandsDestination, ServiceName, _assemblyNameContains),
                DestinationFactory.CreatePublisher<IEvent>(_brokerUri, EventsDestination, ServiceName),
                DestinationFactory.CreatePublisher<IResponse>(_brokerUri, ResponsesDestination, ServiceName),
                typeof(TServiceMessage));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(
                DestinationFactory.CreateSource<IEvent, TServiceMessage>(_brokerUri, EventsDestination, ServiceName, _assemblyNameContains),
                DestinationFactory.CreateSource<IResponse, TServiceMessage>(_brokerUri, ResponsesDestination, ServiceName, _assemblyNameContains),
                DestinationFactory.CreatePublisher<IRequest>(_brokerUri, RequestsDestination, ServiceName),
                DestinationFactory.CreatePublisher<ICommand>(_brokerUri, CommandsDestination, ServiceName),
                typeof(TServiceMessage));
        }
    }
}