using System;
using System.Threading.Tasks;

namespace Obvs.Kafka.Configuration
{
    internal class DisposingServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        private readonly IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> _endpointClient;
        private readonly IDisposable _disposable;

        public DisposingServiceEndpointClient(IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpointClient, IDisposable disposable)
        {
            _endpointClient = endpointClient;
            _disposable = disposable;
        }

        public void Dispose()
        {
            _endpointClient.Dispose();
            _disposable.Dispose();
        }

        public bool CanHandle(TMessage message)
        {
            return _endpointClient.CanHandle(message);
        }

        public string Name
        {
            get { return _endpointClient.Name; }
        }

        public Task SendAsync(TCommand command)
        {
            return _endpointClient.SendAsync(command);
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            return _endpointClient.GetResponses(request);
        }

        public IObservable<TEvent> Events
        {
            get { return _endpointClient.Events; }
        }
    }
}