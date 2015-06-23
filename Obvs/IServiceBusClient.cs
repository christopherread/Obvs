using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceBusClient : IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    public interface IServiceBusClient<TMessage, in TCommand, out TEvent, in TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        IObservable<TEvent> Events { get; }

        Task SendAsync(TCommand command);
        Task SendAsync(IEnumerable<TCommand> commands);

        IObservable<TResponse> GetResponses(TRequest request);
        IObservable<T> GetResponses<T>(TRequest request) where T : TResponse;

        IObservable<Exception> Exceptions { get; }
    }

    public class ServiceBusClient : IServiceBusClient, IDisposable
    {
        private readonly IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBusClient;

        public ServiceBusClient(IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        public IObservable<IEvent> Events
        {
            get { return _serviceBusClient.Events; }
        }

        public Task SendAsync(ICommand command)
        {
            return _serviceBusClient.SendAsync(command);
        }

        public Task SendAsync(IEnumerable<ICommand> commands)
        {
            return _serviceBusClient.SendAsync(commands);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _serviceBusClient.GetResponses(request);
        }

        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse
        {
            return _serviceBusClient.GetResponses<T>(request);
        }

        public IObservable<Exception> Exceptions
        {
            get { return _serviceBusClient.Exceptions; }
        }

        public void Dispose()
        {
            ((IDisposable)_serviceBusClient).Dispose();
        }
    }

    public class ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceBusErrorHandlingBase<TMessage, TCommand, TEvent, TRequest, TResponse>, IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpointClients;
        private readonly IObservable<TEvent> _events;
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;

        public ServiceBusClient(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
        {
            _endpointClients = endpointClients.ToArray();
            _events = _endpointClients.Select(EventsWithErroHandling).Merge().Publish().RefCount();
            _requestCorrelationProvider = requestCorrelationProvider;
        }

        public IObservable<TEvent> Events
        {
            get { return _events; }
        }

        public Task SendAsync(TCommand command)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(command).Select(endpoint => Catch(() => endpoint.SendAsync(command), exceptions, CommandErrorMessage(endpoint))).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(command), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        public Task SendAsync(IEnumerable<TCommand> commands)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = commands.ToArray().Select(command => Catch(() => SendAsync(command), exceptions)).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(), exceptions.Cast<AggregateException>().SelectMany(e => e.InnerExceptions));
            }

            return Task.WhenAll(tasks);
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            if (_requestCorrelationProvider == null)
            {
                throw new InvalidOperationException("Please configure the ServiceBus with a IRequestCorrelationProvider using the fluent configuration extension method .CorrelatesRequestWith()");
            }

            _requestCorrelationProvider.SetRequestCorrelationIds(request);

            return EndpointsThatCanHandle(request).Select(endpoint => endpoint.GetResponses(request)
                                                  .Where(response => _requestCorrelationProvider.AreCorrelated(request, response)))
                                                  .Merge().Publish().RefCount();
        }

        public IObservable<T> GetResponses<T>(TRequest request) where T : TResponse
        {
            IObservable<TResponse> observable = GetResponses(request);
            return observable.OfType<T>();
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointsThatCanHandle(TMessage message)
        {
            return _endpointClients.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpointClient in _endpointClients)
            {
                endpointClient.Dispose();
            }
        }
    }
}