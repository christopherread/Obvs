using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Obvs.Monitoring
{
    internal class ServiceEndpointMonitoringProxy<TMessage, TCommand, TEvent, TRequest, TResponse> :
        IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TResponse : TMessage
        where TRequest : TMessage
        where TEvent : TMessage
        where TCommand : TMessage
        where TMessage : class
    {
        private readonly IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _endpoint;
        private readonly IMonitor<TMessage> _monitor;

        public ServiceEndpointMonitoringProxy(IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, IMonitorFactory<TMessage> monitorFactory)
        {
            if (endpoint == null)
            {
                throw new NullReferenceException("endpoint cannot be null");
            }
            if (monitorFactory == null)
            {
                throw new NullReferenceException("monitorFactory cannot be null");
            }

            _endpoint = endpoint;
            _monitor = monitorFactory.Create(endpoint.Name);
        }

        public void Dispose()
        {
            _endpoint.Dispose();
            _monitor.Dispose();
        }

        public bool CanHandle(TMessage message)
        {
            return _endpoint.CanHandle(message);
        }

        public string Name
        {
            get { return _endpoint.Name; }
        }

        public IObservable<TRequest> Requests
        {
            get
            {
                return Observable.Create<TRequest>(observer =>
                {
                    return _endpoint.Requests.Subscribe(request =>
                    {
                        var stopwatch = Stopwatch.StartNew();
                        observer.OnNext(request);
                        _monitor.MessageReceived(request, stopwatch.Elapsed);
                    }, observer.OnError, observer.OnCompleted);
                });
            }
        }

        public IObservable<TCommand> Commands
        {
            get
            {
                return Observable.Create<TCommand>(observer =>
                {
                    return _endpoint.Commands.Subscribe(command =>
                    {
                        var stopwatch = Stopwatch.StartNew();
                        observer.OnNext(command);
                        _monitor.MessageReceived(command, stopwatch.Elapsed);
                    }, observer.OnError, observer.OnCompleted);
                });
            }
        }

        public Task PublishAsync(TEvent ev)
        {
            var stopwatch = Stopwatch.StartNew();
            return _endpoint.PublishAsync(ev)
                .ContinueWith(_ =>
                {
                    stopwatch.Stop();
                    _monitor.MessageSent(ev, stopwatch.Elapsed);
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        public Task ReplyAsync(TRequest request, TResponse response)
        {
            var stopwatch = Stopwatch.StartNew();
            return _endpoint.ReplyAsync(request, response)
                .ContinueWith(_ =>
                {
                    stopwatch.Stop();
                    _monitor.MessageSent(response, stopwatch.Elapsed);
                }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}