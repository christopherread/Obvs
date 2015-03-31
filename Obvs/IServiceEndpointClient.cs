using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceEndpointClient : IEndpoint
    {
        IObservable<IEvent> Events { get; }
        Task SendAsync(ICommand command);

        IObservable<IResponse> GetResponses(IRequest request);
    }

    public class ServiceEndpointClient : IServiceEndpointClient
    {
        private readonly IMessageSource<IEvent> _eventSource;
        private readonly IMessageSource<IResponse> _responseSource;
        private readonly IMessagePublisher<IRequest> _requestPublisher;
        private readonly IMessagePublisher<ICommand> _commandPublisher;
        private readonly Type _serviceType;

        public ServiceEndpointClient(IMessageSource<IEvent> eventSource,
            IMessageSource<IResponse> responseSource,
            IMessagePublisher<IRequest> requestPublisher,
            IMessagePublisher<ICommand> commandPublisher,
            Type serviceType)
        {
            _eventSource = eventSource;
            _responseSource = responseSource;
            _requestPublisher = requestPublisher;
            _commandPublisher = commandPublisher;
            _serviceType = serviceType;
        }

        public IObservable<IEvent> Events
        {
            get { return _eventSource.Messages; }
        }

        public Task SendAsync(ICommand command)
        {
            return _commandPublisher.PublishAsync(command);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            request.RequestId = Guid.NewGuid().ToString();
            request.RequesterId = RequesterId.Create();

            return Observable.Create<IResponse>(observer =>
            {
                IDisposable disposable = _responseSource.Messages
                    .Where(response => response.RequestId == request.RequestId &&
                                       response.RequesterId == request.RequesterId)
                    .Subscribe(observer);

                _requestPublisher.PublishAsync(request);

                return disposable;
            });
        }

        public bool CanHandle(IMessage message)
        {
            return _serviceType.IsInstanceOfType(message);
        }
    }
}