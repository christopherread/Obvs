using Obvs.Types;

namespace Obvs.Configuration
{
    public interface IServiceEndpointProvider
    {
        IServiceEndpoint CreateEndpoint();
        IServiceEndpointClient CreateEndpointClient();
    }

    public abstract class ServiceEndpointProviderBase : IServiceEndpointProvider
    {
        protected string ServiceName { get; private set; }
        protected readonly string EventsDestination;
        protected readonly string CommandsDestination;
        protected readonly string RequestsDestination;
        protected readonly string ResponsesDestination;

        protected ServiceEndpointProviderBase(string serviceName)
        {
            ServiceName = serviceName;
            EventsDestination = string.Format("{0}.Events", serviceName);
            CommandsDestination = string.Format("{0}.Commands", serviceName);
            RequestsDestination = string.Format("{0}.Requests", serviceName);
            ResponsesDestination = string.Format("{0}.Responses", serviceName);
        }

        public virtual IServiceEndpoint CreateEndpoint()
        {
            return new ServiceEndpoint(new DefaultMessageSource<IRequest>(), new DefaultMessageSource<ICommand>(), new DefaultMessagePublisher<IEvent>(), new DefaultMessagePublisher<IResponse>(), typeof(IMessage));
        }

        public virtual IServiceEndpointClient CreateEndpointClient()
        {
            return new ServiceEndpointClient(new DefaultMessageSource<IEvent>(), new DefaultMessageSource<IResponse>(), new DefaultMessagePublisher<IRequest>(), new DefaultMessagePublisher<ICommand>(), typeof(IMessage));
        }
    }
}