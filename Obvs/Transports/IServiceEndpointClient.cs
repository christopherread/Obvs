using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs.Transports
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
        /// <param name="headers">The headers to send with it, which include the trace context</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task SendAsync(TCommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

        /// <summary>Sends <paramref name="request"/> when subscribed to, and returns the service's responses. If the send fails, the stream ends with its error.</summary>
        /// <param name="request">The request</param>
        /// <param name="headers">The headers to send with it, which include the trace context</param>
        IObservable<TResponse> GetResponses(TRequest request, IReadOnlyDictionary<string, string> headers);
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
        public Task SendAsync(TCommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _commandPublisher.PublishAsync(command, headers, cancellationToken);
        }

        /// <inheritdoc />
        public IObservable<TResponse> GetResponses(TRequest request, IReadOnlyDictionary<string, string> headers)
        {
            return Observable.Create<TResponse>(observer =>
            {
                // Subscribed first, so a response can't arrive before anything is listening
                IDisposable disposable = _responseSource.Messages.Subscribe(observer);

                Task send;
                try
                {
                    send = _requestPublisher.PublishAsync(request, headers);
                }
                catch (Exception exception)
                {
                    send = Task.FromException(exception);
                }
                RequestSends.Started(send);
                _ = ReportSendFailureAsync(send, observer);

                return disposable;
            });
        }

        // A request that wasn't sent won't be answered, so the stream ends with the send's error
        private static async Task ReportSendFailureAsync(Task send, IObserver<TResponse> observer)
        {
            try
            {
                await send.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }
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