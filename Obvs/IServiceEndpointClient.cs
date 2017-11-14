using System;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceEndpointClient : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    public interface IServiceEndpointClient<in TMessage, in TCommand, out TEvent, in TRequest, out TResponse> : IEndpoint<TMessage>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        IObservable<TEvent> Events { get; }
        Task SendAsync(TCommand command);

        IObservable<TResponse> GetResponses(TRequest request);
    }

    public class ServiceEndpointClient : ServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpointClient
    {
        public ServiceEndpointClient(IMessageSource<IEvent> eventSource, IMessageSource<IResponse> responseSource, IMessagePublisher<IRequest> requestPublisher, IMessagePublisher<ICommand> commandPublisher, Type serviceType) 
            : base(eventSource, responseSource, requestPublisher, commandPublisher, serviceType)
        {
        }
    }

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

        public IObservable<TEvent> Events
        {
            get { return _eventSource.Messages; }
        }

        public Task SendAsync(TCommand command)
        {
            return _commandPublisher.PublishAsync(command);
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            return Observable.Create<TResponse>(observer =>
            {
                IDisposable disposable = _responseSource.Messages.Subscribe(observer);

                _requestPublisher.PublishAsync(request);

                return disposable;
            });
        }

        public bool CanHandle(TMessage message)
        {
            return _serviceType.IsInstanceOfType(message);
        }

        public string Name
        {
            get { return _name; }
        }

        public void Dispose()
        {
            _commandPublisher.Dispose();
            _eventSource.Dispose();
            _requestPublisher.Dispose();
            _responseSource.Dispose();
        }
    }
}