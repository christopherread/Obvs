using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Obvs.Configuration;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceBus : IServiceBusClient
    {
        IObservable<IRequest> Requests { get; }
        IObservable<ICommand> Commands { get; }

        void Publish(IEvent ev);
        void Reply(IRequest request, IResponse response);
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

        public void Publish(IEvent ev)
        {
            List<Exception> exceptions = new List<Exception>();

            EndpointsThatCanHandle(ev).ForEach(endpoint => Catch(() => endpoint.Publish(ev), exceptions, EventErrorMessage(endpoint)));

            if (exceptions.Any())
            {
                throw new AggregateException(EventErrorMessage(ev), exceptions);
            }
        }

        public void Reply(IRequest request, IResponse response)
        {
            response.SetCorrelationIds(request);

            List<Exception> exceptions = new List<Exception>();

            EndpointsThatCanHandle(response).ForEach(endpoint => Catch(() => endpoint.Reply(request, response), exceptions, ReplyErrorMessage(endpoint)));

            if (exceptions.Any())
            {
                throw new AggregateException(ReplyErrorMessage(request, response), exceptions);
            }
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