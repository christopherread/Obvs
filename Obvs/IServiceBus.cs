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
    public interface IServiceBus : IServiceBusClient, IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    public interface IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        IObservable<TRequest> Requests { get; }
        IObservable<TCommand> Commands { get; }

        Task PublishAsync(TEvent ev);
        Task ReplyAsync(TRequest request, TResponse response);
    }

    public class ServiceBus : IServiceBus, IDisposable
    {
        private readonly IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBus;

        // ReSharper disable once UnusedMember.Global
        public ServiceBus(IEnumerable<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>> endpointClients, 
            IEnumerable<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>> endpoints, 
            IRequestCorrelationProvider<IRequest, IResponse> requestCorrelationProvider = null) :
            this(new ServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>(endpointClients, endpoints, requestCorrelationProvider ?? new DefaultRequestCorrelationProvider()))
        {
        }

        public ServiceBus(IServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBus)
        {
            _serviceBus = serviceBus;
        }

        // ReSharper disable once UnusedMember.Global
        public static ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> Configure()
        {
            return new ServiceBusFluentCreator<IMessage, ICommand, IEvent, IRequest, IResponse>(new DefaultRequestCorrelationProvider());
        }

        public IObservable<IEvent> Events => _serviceBus.Events;

        public Task SendAsync(ICommand command)
        {
            return _serviceBus.SendAsync(command);
        }

        public Task SendAsync(IEnumerable<ICommand> commands)
        {
            return _serviceBus.SendAsync(commands);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _serviceBus.GetResponses(request);
        }

        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse
        {
            return _serviceBus.GetResponses<T>(request);
        }

        public IObservable<T> GetResponse<T>(IRequest request) where T : IResponse
        {
            return _serviceBus.GetResponse<T>(request);
        }

        public IObservable<Exception> Exceptions => _serviceBus.Exceptions;

        public IObservable<IRequest> Requests => _serviceBus.Requests;

        public IObservable<ICommand> Commands => _serviceBus.Commands;

        public Task PublishAsync(IEvent ev)
        {
            return _serviceBus.PublishAsync(ev);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            return _serviceBus.ReplyAsync(request, response);
        }

        public void Dispose()
        {
            ((IDisposable)_serviceBus).Dispose();
        }
    }

    public class ServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>, IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;

        public ServiceBus(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, 
                          IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> endpoints, 
                          IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider, 
                          IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> localBus = null, LocalBusOptions localBusOption = LocalBusOptions.MessagesWithNoEndpointClients)
            : base(endpointClients, endpoints, requestCorrelationProvider, localBus, localBusOption)
        {
            _requestCorrelationProvider = requestCorrelationProvider;

            Requests = Endpoints
                .Select(endpoint => endpoint.RequestsWithErrorHandling(_exceptions))
                .Merge()
                .Merge(GetLocalRequests())
                .PublishRefCountRetriable();

            Commands = Endpoints
                .Select(endpoint => endpoint.CommandsWithErrorHandling(_exceptions))
                .Merge()
                .Merge(GetLocalCommands())
                .PublishRefCountRetriable();
        }

        public IObservable<TRequest> Requests { get; }

        public IObservable<TCommand> Commands { get; }

        public Task PublishAsync(TEvent ev)
        {
            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(ev)
                .Select(endpoint => Catch(() => endpoint.PublishAsync(ev), exceptions, EventErrorMessage(endpoint)))
                .Union(PublishLocal(ev, exceptions))
                .ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(EventErrorMessage(ev), exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new Exception(
                    $"No endpoint or local bus configured for {ev}, please check your ServiceBus configuration.");
            }

            return Task.WhenAll(tasks);
        }

        public Task ReplyAsync(TRequest request, TResponse response)
        {
            if (_requestCorrelationProvider == null)
            {
                throw new InvalidOperationException("Please configure the ServiceBus with a IRequestCorrelationProvider using the fluent configuration extension method .CorrelatesRequestWith()");
            }

            _requestCorrelationProvider.SetCorrelationIds(request, response);

            var exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(response)
                    .Select(endpoint => Catch(() => endpoint.ReplyAsync(request, response), exceptions, ReplyErrorMessage(endpoint)))
                    .Union(ReplyLocal(request, response, exceptions))
                    .ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(ReplyErrorMessage(request, response), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        // ReSharper disable once UnusedMember.Global
        public static ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> Configure()
        {
            return new ServiceBusFluentCreator<TMessage, TCommand, TEvent, TRequest, TResponse>();
        }

        private IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointsThatCanHandle(TMessage message)
        {
            return Endpoints.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (var endpoint in Endpoints)
            {
                endpoint.Dispose();
            }
        }
    }
}