using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Extensions;

namespace Obvs.Monitoring
{
    internal class ServiceEndpointClientMonitoringProxy<TMessage, TCommand, TEvent, TRequest, TResponse> : 
        IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> 
        where TResponse : TMessage 
        where TRequest : TMessage 
        where TEvent : TMessage 
        where TCommand : TMessage 
        where TMessage : class
    {
        private readonly IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> _endpointClient;
        private readonly IMonitor<TMessage> _monitor;

        public ServiceEndpointClientMonitoringProxy(IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpointClient, IMonitorFactory<TMessage> monitorFactory)
        {
            if (endpointClient == null)
            {
                throw new NullReferenceException("endpointClient cannot be null");
            }
            if (monitorFactory == null)
            {
                throw new NullReferenceException("monitorFactory cannot be null");
            }

            _endpointClient = endpointClient;
            _monitor = monitorFactory.Create(endpointClient.Name);
       }

        public void Dispose()
        {
            _endpointClient.Dispose();
            _monitor.Dispose();
        }

        public bool CanHandle(TMessage message)
        {
            return _endpointClient.CanHandle(message);
        }

        public string Name
        {
            get { return _endpointClient.Name; }
        }

        public IObservable<TEvent> Events
        {
            get
            {
                return Observable.Create<TEvent>(observer =>
                {
                    return _endpointClient.Events.Subscribe(ev =>
                    {
                        var stopwatch = Stopwatch.StartNew();
                        observer.OnNext(ev);
                        _monitor.MessageReceived(ev, stopwatch.Elapsed);
                    }, observer.OnError, observer.OnCompleted);
                });
            }
        }

        public Task SendAsync(TCommand command)
        {
            var stopwatch = Stopwatch.StartNew();
            return _endpointClient.SendAsync(command)
                .ContinueWith(_ =>
                {
                    stopwatch.Stop();
                    _monitor.MessageSent(command, stopwatch.Elapsed);
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            return Observable.Create<TResponse>(observer =>
            {
                var stopwatch = Stopwatch.StartNew();
                var observable = _endpointClient.GetResponses(request);
                _monitor.MessageSent(request, stopwatch.Elapsed);

                return observable.Subscribe(
                    response =>
                    {
                        var stopwatch2 = Stopwatch.StartNew();
                        observer.OnNext(response);
                        _monitor.MessageReceived(response, stopwatch2.Elapsed);
                    }, observer.OnError, observer.OnCompleted);
            });
        }
    }
}