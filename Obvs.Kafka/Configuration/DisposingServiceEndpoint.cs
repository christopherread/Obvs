using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Transports;

namespace Obvs.Kafka.Configuration
{
    internal class DisposingServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class 
        where TCommand : TMessage 
        where TEvent : TMessage 
        where TRequest : TMessage 
        where TResponse : TMessage
    {
        private readonly IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _endpoint;
        private readonly IDisposable _disposable;

        public DisposingServiceEndpoint(IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IDisposable disposable)
        {
            _endpoint = endpoint;
            _disposable = disposable;
        }

        public void Dispose()
        {
            _endpoint.Dispose();
            _disposable.Dispose();
        }

        public bool CanHandle(TMessage message)
        {
            return _endpoint.CanHandle(message);
        }

        public string Name
        {
            get { return _endpoint.Name; }
        }

        public Task PublishAsync(TEvent ev, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _endpoint.PublishAsync(ev, headers, cancellationToken);
        }

        public Task ReplyAsync(TRequest request, TResponse response, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            return _endpoint.ReplyAsync(request, response, headers, cancellationToken);
        }

        public IObservable<TRequest> Requests
        {
            get { return _endpoint.Requests; }
        }

        public IObservable<TCommand> Commands
        {
            get { return _endpoint.Commands; }
        }
    }
}