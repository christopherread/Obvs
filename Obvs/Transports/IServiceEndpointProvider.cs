using Obvs.Types;

namespace Obvs.Transports
{
    /// <summary>Creates a service's endpoints, for the bus's standard message interfaces.</summary>
    public interface IServiceEndpointProvider : IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    /// <summary>
    /// A transport's factory for one service's endpoints: <see cref="CreateEndpoint"/> for an app that's the
    /// service's server, and <see cref="CreateEndpointClient"/> for one that's its client. Each transport's
    /// <c>Use&lt;Transport&gt;()</c> registers one with <c>UseEndpointProvider</c>.
    /// </summary>
    public interface IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : TMessage 
        where TEvent : TMessage 
        where TRequest : TMessage 
        where TResponse : TMessage
    {
        /// <summary>The server's side of the service: it receives commands and requests, and publishes events and responses.</summary>
        IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint();
        /// <summary>The client's side of the service: it sends commands and requests, and receives events and responses.</summary>
        IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient();
    }

    /// <summary>A base for endpoint providers, for the bus's standard message interfaces.</summary>
    public abstract class ServiceEndpointProviderBase : ServiceEndpointProviderBase<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpointProvider
    {
        /// <summary>Names the service's destinations after <paramref name="serviceName"/>.</summary>
        /// <param name="serviceName">The service's name</param>
        protected ServiceEndpointProviderBase(string serviceName) : base(serviceName)
        {
        }
    }

    /// <summary>A base for endpoint providers, whose endpoints do nothing unless overridden, with the service's destinations named after it.</summary>
    public abstract class ServiceEndpointProviderBase<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>The service's name.</summary>
        protected string ServiceName { get; private set; }
        /// <summary>Where the service's events go: <c>{service}.Events</c>.</summary>
        protected readonly string EventsDestination;
        /// <summary>Where the service's commands go: <c>{service}.Commands</c>.</summary>
        protected readonly string CommandsDestination;
        /// <summary>Where the service's requests go: <c>{service}.Requests</c>.</summary>
        protected readonly string RequestsDestination;
        /// <summary>Where the service's responses go: <c>{service}.Responses</c>.</summary>
        protected readonly string ResponsesDestination;

        /// <summary>Names the service's destinations after <paramref name="serviceName"/>.</summary>
        /// <param name="serviceName">The service's name</param>
        protected ServiceEndpointProviderBase(string serviceName)
        {
            ServiceName = serviceName;
            EventsDestination = string.Format("{0}.Events", serviceName);
            CommandsDestination = string.Format("{0}.Commands", serviceName);
            RequestsDestination = string.Format("{0}.Requests", serviceName);
            ResponsesDestination = string.Format("{0}.Responses", serviceName);
        }

        /// <inheritdoc />
        public virtual IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpoint()
        {
            return new ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>(new DefaultMessageSource<TRequest>(), new DefaultMessageSource<TCommand>(), new DefaultMessagePublisher<TEvent>(), new DefaultMessagePublisher<TResponse>(), typeof(TMessage));
        }

        /// <inheritdoc />
        public virtual IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateEndpointClient()
        {
            return new ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>(new DefaultMessageSource<TEvent>(), new DefaultMessageSource<TResponse>(), new DefaultMessagePublisher<TRequest>(), new DefaultMessagePublisher<TCommand>(), typeof(TMessage));
        }
    }
}