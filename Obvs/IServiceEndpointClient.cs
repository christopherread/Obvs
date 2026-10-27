using System;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    /// <summary>A service's client endpoint, for the bus's standard message interfaces.</summary>
    public interface IServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    /// <summary>A service's client endpoint: it sends commands and requests, and receives events and responses.</summary>
    public interface IServiceEndpointClient<in TMessage, in TCommand, out TEvent, in TRequest, out TResponse> : IEndpoint<TMessage>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        /// <summary>The service's events.</summary>
        IObservable<TEvent> Events { get; }
        /// <summary>Sends <paramref name="command"/> to the service.</summary>
        /// <param name="command">The command</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task SendAsync(TCommand command, CancellationToken cancellationToken = default);

        /// <summary>Sends <paramref name="request"/> when subscribed to, and returns the service's responses.</summary>
        IObservable<TResponse> GetResponses(TRequest request);
    }

    /// <summary>A service's client endpoint, for the bus's standard message interfaces.</summary>
    public class ServiceEndpointClient : ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpointClient
    {
        /// <summary>An endpoint over these sources and publishers.</summary>
        /// <param name="eventSource">Where events arrive</param>
        /// <param name="responseSource">Where responses arrive</param>
        /// <param name="requestPublisher">Where requests go</param>
        /// <param name="commandPublisher">Where commands go</param>
        /// <param name="serviceType">The service's message interface</param>
        public ServiceEndpointClient(IMessageSource<IEvent> eventSource, IMessageSource<IResponse> responseSource, IMessagePublisher<IRequest> requestPublisher, IMessagePublisher<ICommand> commandPublisher, Type serviceType) 
            : base(eventSource, responseSource, requestPublisher, commandPublisher, serviceType)
        {
        }
    }

    /// <summary>A service's client endpoint, over a transport's sources and publishers.</summary>
    public class ServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IMessageSource<TEvent> _eventSource;
        private readonly IMessageSource<TResponse> _responseSource;
        private readonly IMessagePublisher<TRequest> _requestPublisher;
        private readonly IMessagePublisher<TCommand> _commandPublisher;
        private readonly TypeInfo _serviceType;
        private readonly string _name;

        /// <summary>An endpoint over these sources and publishers.</summary>
        /// <param name="eventSource">Where events arrive</param>
        /// <param name="responseSource">Where responses arrive</param>
        /// <param name="requestPublisher">Where requests go</param>
        /// <param name="commandPublisher">Where commands go</param>
        /// <param name="serviceType">The service's message interface</param>
        public ServiceEndpointClient(IMessageSource<TEvent> eventSource,
            IMessageSource<TResponse> responseSource,
            IMessagePublisher<TRequest> requestPublisher,
            IMessagePublisher<TCommand> commandPublisher,
            Type serviceType)
        {
            _eventSource = eventSource;
            _responseSource = responseSource;
            _requestPublisher = requestPublisher;
            _commandPublisher = commandPublisher;
            _serviceType = serviceType.GetTypeInfo();
            _name = string.Format("{0}[{1}]", GetType().GetTypeInfo().GetSimpleName(), _serviceType.Name);
        }

        /// <inheritdoc />
        public IObservable<TEvent> Events
        {
            get { return _eventSource.Messages; }
        }

        /// <inheritdoc />
        public Task SendAsync(TCommand command, CancellationToken cancellationToken = default)
        {
            return _commandPublisher.PublishAsync(command, cancellationToken);
        }

        /// <inheritdoc />
        public IObservable<TResponse> GetResponses(TRequest request)
        {
            return Observable.Create<TResponse>(observer =>
            {
                IDisposable disposable = _responseSource.Messages.Subscribe(observer);

                _requestPublisher.PublishAsync(request);

                return disposable;
            });
        }

        /// <inheritdoc />
        public bool CanHandle(TMessage message)
        {
            return _serviceType.IsInstanceOfType(message);
        }

        /// <inheritdoc />
        public string Name
        {
            get { return _name; }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _commandPublisher.Dispose();
            _eventSource.Dispose();
            _requestPublisher.Dispose();
            _responseSource.Dispose();
        }
    }
}