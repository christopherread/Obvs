using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Extensions;
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
        IObservable<T> GetResponse<T>(TRequest request) where T : TResponse;

        IObservable<Exception> Exceptions { get; }
    }

    public class ServiceBusClient : IServiceBusClient, IDisposable
    {
        private readonly IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBusClient;

        public ServiceBusClient(IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        public IObservable<IEvent> Events => _serviceBusClient.Events;

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

        public IObservable<T> GetResponse<T>(IRequest request) where T : IResponse
        {
            return _serviceBusClient.GetResponse<T>(request);
        }

        public IObservable<Exception> Exceptions => _serviceBusClient.Exceptions;

        public void Dispose()
        {
            ((IDisposable)_serviceBusClient).Dispose();
        }
    }

    /// <summary> merges multiple configured <see cref="IServiceEndpoint"/>s and <see cref="IServiceEndpointClient"/>s into an Observable stream per message type </summary>
    public class ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceBusErrorHandlingBase<TMessage, TCommand, TEvent, TRequest, TResponse>
        , IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        protected readonly IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> Endpoints;
        private readonly IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpointClients;
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;
        private readonly IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> _localBus;
        private readonly LocalBusOptions _localBusOption;

        public ServiceBusClient(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, 
                                IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> endpoints, 
                                IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider,
                                IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> localBus = null, LocalBusOptions localBusOption = LocalBusOptions.MessagesWithNoEndpointClients)
        {
            _localBus = localBus;
            _localBusOption = localBusOption;

            Endpoints = endpoints.ToList();

            _endpointClients = endpointClients.ToArray();

            Events = _endpointClients
                .Select(endpointClient => endpointClient.EventsWithErrorHandling(_exceptions))
                .Merge()
                .Merge(GetLocalEvents())
                .PublishRefCountRetriable();

            _requestCorrelationProvider = requestCorrelationProvider;
        }

        public IObservable<TEvent> Events { get; }

        public Task SendAsync(TCommand command)
        {
            var exceptions = new List<Exception>();

            var tasks = EndpointClientsThatCanHandle(command)
                .Select(endpoint => Catch(() => endpoint.SendAsync(command), exceptions, CommandErrorMessage(endpoint)))
                .Union(SendLocal(command, exceptions))
                .ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(command), exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new Exception(
                    $"No endpoint or local bus configured for {command}, please check your ServiceBus configuration.");
            }

            return Task.WhenAll(tasks);
        }

        private IObservable<TEvent> GetLocalEvents()
        {
            return _localBus == null ? Observable.Empty<TEvent>() : _localBus.Events;
        }

        protected IObservable<TCommand> GetLocalCommands()
        {
            return _localBus == null ? Observable.Empty<TCommand>() : _localBus.Commands;
        }

        protected IObservable<TRequest> GetLocalRequests()
        {
            return _localBus == null ? Observable.Empty<TRequest>() : _localBus.Requests;
        }

        protected IEnumerable<Task> PublishLocal(TEvent ev, List<Exception> exceptions)
        {
            return ShouldPublishLocally(ev)
                ? new[] {Catch(() => _localBus.PublishAsync(ev), exceptions)} 
                : Enumerable.Empty<Task>();
        }

        private IEnumerable<Task> SendLocal(TCommand command, List<Exception> exceptions)
        {
            return ShouldPublishLocally(command) 
                ? new[] { Catch(() => _localBus.SendAsync(command), exceptions) } 
                : Enumerable.Empty<Task>();
        }

        protected IEnumerable<Task> ReplyLocal(TRequest request, TResponse response, List<Exception> exceptions)
        {
            return ShouldPublishLocally(response)
                ? new[] { Catch(() => _localBus.ReplyAsync(request, response), exceptions) }
                : Enumerable.Empty<Task>();
        }

        private bool ShouldPublishLocally(TMessage message)
        {
            if (_localBus == null)
            {
                return false;
            }

            if  (_localBusOption == LocalBusOptions.AllMessages)
            {
                return true;
            }

            if (_localBusOption == LocalBusOptions.MessagesWithNoEndpointClients &&
                !_endpointClients.Any(e => e.CanHandle(message)))
            {
                return true;
            }

            if (_localBusOption == LocalBusOptions.MessagesWithNoEndpoints && 
                !Endpoints.Any(e => e.CanHandle(message)) &&
                !_endpointClients.Any(e => e.CanHandle(message)))
            {
                return true;
            }

            return false;
        }

        public Task SendAsync(IEnumerable<TCommand> commands)
        {
            var commandsResolved = commands.ToArray();

            if (commandsResolved.Length == 0)
                return Task.FromResult(true);

            var exceptions = new List<Exception>();
            
            var tasks = commandsResolved.Select(command => Catch(() => SendAsync(command), exceptions)).ToArray();

            if (exceptions.Any())
            {
                Exception[] GetInnerExceptions(Exception e) =>
                    e is AggregateException aggregateException
                        ? aggregateException.InnerExceptions.ToArray()
                        : new[] {e};

                throw new AggregateException(CommandErrorMessage(), exceptions.SelectMany(GetInnerExceptions));
            }

            if (tasks.Length == 0)
            {
                throw new Exception("No endpoint or local bus configured for any of these commands, please check your ServiceBus configuration.");
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

            return EndpointClientsThatCanHandle(request)
                .Select(endpoint => endpoint.GetResponses(request)
                .Where(response => _requestCorrelationProvider.AreCorrelated(request, response)))
                .Merge()
                .Merge(GetLocalResponses(request))
                .PublishRefCountRetriable();
        }

        private IObservable<TResponse> GetLocalResponses(TRequest request)
        {
            return ShouldPublishLocally(request)
                ? _localBus.GetResponses(request)
                : Observable.Empty<TResponse>();
        }

        public IObservable<T> GetResponses<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>();
        }

        public IObservable<T> GetResponse<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>().Take(1);
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointClientsThatCanHandle(TMessage message)
        {
            return _endpointClients.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (var endpointClient in _endpointClients)
            {
                endpointClient.Dispose();
            }
        }
    }
}