using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Transports;

namespace Obvs.RabbitMQ.Configuration
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

        public Task SendAsync(TCommand command, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _endpointClient.SendAsync(command, headers, cancellationToken);
        }

        public IObservable<TResponse> GetResponses(TRequest request, IReadOnlyDictionary<string, string> headers)
        {
            return _endpointClient.GetResponses(request, headers);
        }

        public IObservable<TEvent> Events
        {
            get { return _endpointClient.Events; }
        }
    }
}