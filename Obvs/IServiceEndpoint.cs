using System;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    public interface IEndpoint
    {
        bool CanHandle(IMessage message);
    }

    public interface IServiceEndpoint : IEndpoint
    {
        IObservable<IRequest> Requests { get; }
        IObservable<ICommand> Commands { get; }

        Task PublishAsync(IEvent ev);
        Task ReplyAsync(IRequest request, IResponse response);
    }

    public class ServiceEndpoint : IServiceEndpoint
    {
        private readonly IMessageSource<IRequest> _requestSource;
        private readonly IMessageSource<ICommand> _commandSource;
        private readonly IMessagePublisher<IEvent> _eventPublisher;
        private readonly IMessagePublisher<IResponse> _responsePublisher;

        public ServiceEndpoint(IMessageSource<IRequest> requestSource,
            IMessageSource<ICommand> commandSource,
            IMessagePublisher<IEvent> eventPublisher,
            IMessagePublisher<IResponse> responsePublisher,
            Type serviceType)
        {
            _requestSource = requestSource;
            _commandSource = commandSource;
            _eventPublisher = eventPublisher;
            _responsePublisher = responsePublisher;
            ServiceType = serviceType;
        }

        public IObservable<IRequest> Requests
        {
            get { return _requestSource.Messages; }
        }

        public IObservable<ICommand> Commands
        {
            get { return _commandSource.Messages; }
        }

        public Task PublishAsync(IEvent ev)
        {
            return _eventPublisher.PublishAsync(ev);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;

            return _responsePublisher.PublishAsync(response);
        }

        public bool CanHandle(IMessage message)
        {
            return ServiceType.IsInstanceOfType(message);
        }

        private Type ServiceType { get; set; }
    }
}