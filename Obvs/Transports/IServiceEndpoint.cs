using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs.Transports
{
    /// <summary>A service's server endpoint, for the bus's standard message interfaces.</summary>
    public interface IServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    /// <summary>A service's server endpoint: it receives commands and requests, and publishes events and replies.</summary>
    public interface IServiceEndpoint<in TMessage, out TCommand, in TEvent, TRequest, in TResponse> : IEndpoint<TMessage>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        /// <summary>Requests awaiting a <see cref="ReplyAsync"/>.</summary>
        IObservable<TRequest> Requests { get; }

        /// <summary>The commands the service receives.</summary>
        IObservable<TCommand> Commands { get; }

        /// <summary>Publishes <paramref name="tEvent"/> to the service's clients.</summary>
        /// <param name="tEvent">The event</param>
        /// <param name="headers">The headers to send with it, which include the trace context</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task PublishAsync(TEvent tEvent, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

        /// <summary>Sends <paramref name="response"/> to whoever sent <paramref name="request"/>, one of <see cref="Requests"/>.</summary>
        /// <param name="request">The request being answered</param>
        /// <param name="response">The answer</param>
        /// <param name="headers">The headers to send with it, which include the trace context</param>
        /// <param name="cancellationToken">Stops waiting for the broker</param>
        Task ReplyAsync(TRequest request, TResponse response, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
    }

    /// <summary>A service's server endpoint, for the bus's standard message interfaces.</summary>
    public class ServiceEndpoint : ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpoint
    {
        /// <summary>An endpoint over these sources and publishers.</summary>
        /// <param name="requestSource">Where requests arrive</param>
        /// <param name="commandSource">Where commands arrive</param>
        /// <param name="eventPublisher">Where events go</param>
        /// <param name="responsePublisher">Where responses go</param>
        /// <param name="serviceType">The service's message interface</param>
        public ServiceEndpoint(IMessageSource<IRequest> requestSource, IMessageSource<ICommand> commandSource, IMessagePublisher<IEvent> eventPublisher, IMessagePublisher<IResponse> responsePublisher, Type serviceType) 
            : base(requestSource, commandSource, eventPublisher, responsePublisher, serviceType)
        {
        }
    }

    /// <summary>A service's server endpoint, over a transport's sources and publishers.</summary>
    public class ServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IMessageSource<TRequest> _requestSource;
        private readonly IMessageSource<TCommand> _commandSource;
        private readonly IMessagePublisher<TEvent> _eventPublisher;
        private readonly IMessagePublisher<TResponse> _responsePublisher;
        private readonly string _name;

        /// <summary>An endpoint over these sources and publishers.</summary>
        /// <param name="requestSource">Where requests arrive</param>
        /// <param name="commandSource">Where commands arrive</param>
        /// <param name="eventPublisher">Where events go</param>
        /// <param name="responsePublisher">Where responses go</param>
        /// <param name="serviceType">The service's message interface</param>
        public ServiceEndpoint(IMessageSource<TRequest> requestSource,
            IMessageSource<TCommand> commandSource,
            IMessagePublisher<TEvent> eventPublisher,
            IMessagePublisher<TResponse> responsePublisher,
            Type serviceType)
        {
            _requestSource = requestSource;
            _commandSource = commandSource;
            _eventPublisher = eventPublisher;
            _responsePublisher = responsePublisher;
            ServiceType = serviceType;
            _name = string.Format("{0}[{1}]", GetType().GetTypeInfo().GetSimpleName(), ServiceType.Name);
        }

        /// <inheritdoc />
        public IObservable<TRequest> Requests
        {
            get { return _requestSource.Messages; }
        }

        /// <inheritdoc />
        public IObservable<TCommand> Commands
        {
            get { return _commandSource.Messages; }
        }

        /// <inheritdoc />
        public Task PublishAsync(TEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _eventPublisher.PublishAsync(ev, headers, cancellationToken);
        }

        /// <inheritdoc />
        public Task ReplyAsync(TRequest request, TResponse response, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _responsePublisher.PublishAsync(response, headers, cancellationToken);
        }

        /// <inheritdoc />
        public bool CanHandle(TMessage message)
        {
            return ServiceType.IsInstanceOfType(message);
        }

        /// <inheritdoc />
        public string Name
        {
            get { return _name; }
        }

        private Type ServiceType { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            _commandSource.Dispose();
            _eventPublisher.Dispose();
            _requestSource.Dispose();
            _responsePublisher.Dispose();
        }
    }
}