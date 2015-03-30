using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceBusClient
    {
        IObservable<IEvent> Events { get; }

        Task SendAsync(ICommand command);
        Task SendAsync(IEnumerable<ICommand> commands);

        IObservable<IResponse> GetResponses(IRequest request);
        IObservable<T> GetResponses<T>(IRequest request) where T : IResponse;

        IObservable<Exception> Exceptions { get; }
    }

    public class ServiceBusClient : ServiceBusErrorHandlingBase, IServiceBusClient
    {
        private readonly IEnumerable<IServiceEndpointClient> _endpointClients;
        private readonly IObservable<IEvent> _events;
        
        public ServiceBusClient(IEnumerable<IServiceEndpointClient> endpointClients)
        {
            _endpointClients = endpointClients.ToArray();
            _events = _endpointClients.Select(EventsWithErroHandling).Merge().Publish().RefCount();
        }

        public IObservable<IEvent> Events
        {
            get { return _events; }
        }

        public Task SendAsync(ICommand command)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(command).Select(endpoint => Catch(() => endpoint.SendAsync(command), exceptions, CommandErrorMessage(endpoint))).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(command), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        public Task SendAsync(IEnumerable<ICommand> commands)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = commands.ToArray().Select(command => Catch(() => SendAsync(command), exceptions)).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(), exceptions.Cast<AggregateException>().SelectMany(e => e.InnerExceptions));
            }

            return Task.WhenAll(tasks);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            request.SetCorrelationIds();

            return EndpointsThatCanHandle(request).Select(endpoint => endpoint.GetResponses(request).Where(response => response.CorrelatesTo(request)))
                                                  .Merge().Publish().RefCount();
        }

        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse
        {
            return GetResponses(request).OfType<T>();
        }

        private List<IServiceEndpointClient> EndpointsThatCanHandle(IMessage message)
        {
            return _endpointClients.Where(endpoint => endpoint.CanHandle(message)).ToList();
        }
    }
}