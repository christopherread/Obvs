using System;
using System.Reflection;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceEndpoint : IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    /// <summary> Groups as service’s sources and publishers together; aggregated into a <see cref="ServiceBus"/> </summary>
    public interface IServiceEndpoint<in TMessage, out TCommand, in TEvent, TRequest, in TResponse> : IEndpoint<TMessage>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        /// <summary> Requests awaiting a <see cref="ReplyAsync(TRequest, TResponse)"/> </summary>
        IObservable<TRequest> Requests { get; }

        /// <summary> Fire-and-Forget Commands from <see cref="PublishAsync(TEvent)"/> </summary>
        IObservable<TCommand> Commands { get; }

        /// <summary> Publishing <paramref name="tEvent"/>s yields corresponding <see cref="Commands"/> </summary>
        Task PublishAsync(TEvent tEvent);

        /// <summary> Used for <paramref name="response"/>s to <paramref name="request"/>s from <see cref="Requests"/> </summary>
        Task ReplyAsync(TRequest request, TResponse response);
    }

    public class ServiceEndpoint : ServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpoint
    {
        public ServiceEndpoint(IMessageSource<IRequest> requestSource, IMessageSource<ICommand> commandSource, IMessagePublisher<IEvent> eventPublisher, IMessagePublisher<IResponse> responsePublisher, Type serviceType) 
            : base(requestSource, commandSource, eventPublisher, responsePublisher, serviceType)
        {
        }
    }

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

        public IObservable<TRequest> Requests
        {
            get { return _requestSource.Messages; }
        }

        public IObservable<TCommand> Commands
        {
            get { return _commandSource.Messages; }
        }

        public Task PublishAsync(TEvent ev)
        {
            return _eventPublisher.PublishAsync(ev);
        }

        public Task ReplyAsync(TRequest request, TResponse response)
        {
            return _responsePublisher.PublishAsync(response);
        }

        public bool CanHandle(TMessage message)
        {
            return ServiceType.IsInstanceOfType(message);
        }

        public string Name
        {
            get { return _name; }
        }

        private Type ServiceType { get; set; }

        public void Dispose()
        {
            _commandSource.Dispose();
            _eventPublisher.Dispose();
            _requestSource.Dispose();
            _responsePublisher.Dispose();
        }
    }
}