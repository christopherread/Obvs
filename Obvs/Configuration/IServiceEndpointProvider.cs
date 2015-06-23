using Obvs.Types;

namespace Obvs.Configuration
{
    public interface IServiceEndpointProvider : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    public interface IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : TMessage 
        where TEvent : TMessage 
        where TRequest : TMessage 
        where TResponse : TMessage
    {
        IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint();
        IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient();
    }

    public abstract class ServiceEndpointProviderBase : ServiceEndpointProviderBase<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpointProvider
    {
        protected ServiceEndpointProviderBase(string serviceName) : base(serviceName)
        {
        }
    }

    public abstract class ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
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

        public virtual IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(new DefaultMessageSource<TRequest>(), new DefaultMessageSource<TCommand>(), new DefaultMessagePublisher<TEvent>(), new DefaultMessagePublisher<TResponse>(), typeof(TMessage));
        }

        public virtual IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(new DefaultMessageSource<TEvent>(), new DefaultMessageSource<TResponse>(), new DefaultMessagePublisher<TRequest>(), new DefaultMessagePublisher<TCommand>(), typeof(TMessage));
        }
    }
}