using System;
using Obvs.Types;

namespace Obvs
{
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

        public void Publish(IEvent ev)
        {
            _eventPublisher.Publish(ev);
        }

        public void Reply(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;

            _responsePublisher.Publish(response);
        }

        public bool CanHandle(IMessage message)
        {
            return ServiceType.IsInstanceOfType(message);
        }

        private Type ServiceType { get; set; }
    }
}