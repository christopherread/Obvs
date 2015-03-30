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
    public interface IServiceBus : IServiceBusClient
    {
        IObservable<IRequest> Requests { get; }
        IObservable<ICommand> Commands { get; }

        Task PublishAsync(IEvent ev);
        Task ReplyAsync(IRequest request, IResponse response);
    }

    public class ServiceBus : ServiceBusClient, IServiceBus
    {
        private readonly IEnumerable<IServiceEndpoint> _endpoints;
        private readonly IObservable<IRequest> _requests;
        private readonly IObservable<ICommand> _commands;

        public ServiceBus(IEnumerable<IServiceEndpointClient> endpointClients, IEnumerable<IServiceEndpoint> endpoints)
            : base(endpointClients)
        {
            _endpoints = endpoints.ToList();
            _requests = _endpoints.Select(RequestsWithErrorHandling).Merge().Publish().RefCount();
            _commands = _endpoints.Select(CommandsWithErrorHandling).Merge().Publish().RefCount();
        }

        public IObservable<IRequest> Requests
        {
            get { return _requests; }
        }

        public IObservable<ICommand> Commands
        {
            get { return _commands; }
        }

        public Task PublishAsync(IEvent ev)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(ev).Select(endpoint => Catch(() => endpoint.PublishAsync(ev), exceptions, EventErrorMessage(endpoint))).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(EventErrorMessage(ev), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            response.SetCorrelationIds(request);

            List<Exception> exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(response).Select(endpoint => Catch(() => endpoint.ReplyAsync(request, response), exceptions, ReplyErrorMessage(endpoint))).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(ReplyErrorMessage(request, response), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        public static ICanAddEndpoint Configure()
        {
            return new ServiceBusFluentCreator();
        }

        private List<IServiceEndpoint> EndpointsThatCanHandle(IMessage message)
        {
            return _endpoints.Where(endpoint => endpoint.CanHandle(message)).ToList();
        }
    }
}