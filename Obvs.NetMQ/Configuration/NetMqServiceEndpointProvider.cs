using NetMQ;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.NetMQ.Configuration
{
    public class NetMqServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly IMessageSerializer _serializer;
        private readonly IMessageDeserializerFactory _deserializerFactory;
        private readonly string _assemblyNameContains;
        private readonly NetMQContext _context = NetMQContext.Create();
        private readonly string _requestAddress;
        private readonly string _responseAddress;
        private readonly string _commandAddress;
        private readonly string _eventAddress;

        public NetMqServiceEndpointProvider(string serviceName, string address, int port, IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory, string assemblyNameContains)
            : base(serviceName)
        {
            _serializer = serializer;
            _deserializerFactory = deserializerFactory;
            _assemblyNameContains = assemblyNameContains;
            _requestAddress = string.Format("{0}:{1}", address, (port + 0));
            _responseAddress = string.Format("{0}:{1}", address, (port + 1));
            _commandAddress = string.Format("{0}:{1}", address, (port + 2));
            _eventAddress = string.Format("{0}:{1}", address, (port + 3));
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(
               new MessageSource<IRequest>(_requestAddress, _deserializerFactory.Create<IRequest, TServiceMessage>(_assemblyNameContains), _context, RequestsDestination),
               new MessageSource<ICommand>(_commandAddress, _deserializerFactory.Create<ICommand, TServiceMessage>(_assemblyNameContains), _context, CommandsDestination),
               new MessagePublisher<IEvent>(_eventAddress, _serializer, _context, EventsDestination),
               new MessagePublisher<IResponse>(_responseAddress, _serializer, _context, ResponsesDestination), 
               typeof(TServiceMessage));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(
               new MessageSource<IEvent>(_eventAddress, _deserializerFactory.Create<IEvent, TServiceMessage>(_assemblyNameContains), _context, EventsDestination),
               new MessageSource<IResponse>(_responseAddress, _deserializerFactory.Create<IResponse, TServiceMessage>(_assemblyNameContains), _context, ResponsesDestination),
               new MessagePublisher<IRequest>(_requestAddress, _serializer, _context, RequestsDestination),
               new MessagePublisher<ICommand>(_commandAddress, _serializer, _context, CommandsDestination), 
               typeof(TServiceMessage));
        }
    }
}