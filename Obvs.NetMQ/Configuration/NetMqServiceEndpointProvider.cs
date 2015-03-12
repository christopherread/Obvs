using NetMQ;
using Obvs.Configuration;
using Obvs.Serialization;
using Obvs.Types;

namespace Obvs.NetMQ.Configuration
{
    public class NetMqServiceEndpointProvider<TServiceMessage> : ServiceEndpointProviderBase where TServiceMessage : IMessage
    {
        private readonly NetMQContext _context = NetMQContext.Create();
        private readonly string _requestAddress;
        private readonly string _responseAddress;
        private readonly string _commandAddress;
        private readonly string _eventAddress;

        public NetMqServiceEndpointProvider(string serviceName, string address, int port)
            : base(serviceName)
        {
            _requestAddress = string.Format("{0}:{1}", address, (port + 0));
            _responseAddress = string.Format("{0}:{1}", address, (port + 1));
            _commandAddress = string.Format("{0}:{1}", address, (port + 2));
            _eventAddress = string.Format("{0}:{1}", address, (port + 3));
        }

        public override IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(
               new MessageSource<IRequest>(_requestAddress, DeserializerTypeFactory.CreateJson<IRequest, TServiceMessage>(), _context, RequestsDestination),
               new MessageSource<ICommand>(_commandAddress, DeserializerTypeFactory.CreateJson<ICommand, TServiceMessage>(), _context, CommandsDestination),
               new MessagePublisher<IEvent>(_eventAddress, new JsonMessageSerializer(), _context, EventsDestination),
               new MessagePublisher<IResponse>(_responseAddress, new JsonMessageSerializer(), _context, ResponsesDestination), 
               typeof(TServiceMessage));
        }

        public override IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(
               new MessageSource<IEvent>(_eventAddress, DeserializerTypeFactory.CreateJson<IEvent, TServiceMessage>(), _context, EventsDestination),
               new MessageSource<IResponse>(_responseAddress, DeserializerTypeFactory.CreateJson<IResponse, TServiceMessage>(), _context, ResponsesDestination),
               new MessagePublisher<IRequest>(_requestAddress, new JsonMessageSerializer(), _context, RequestsDestination),
               new MessagePublisher<ICommand>(_commandAddress, new JsonMessageSerializer(), _context, CommandsDestination), 
               typeof(TServiceMessage));
        }
    }
}