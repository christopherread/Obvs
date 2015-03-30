using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Logging
{
    public class ServiceEndpointClientLoggingProxy : IServiceEndpointClient
    {
        private readonly IServiceEndpointClient _endpoint;
        private readonly ILogger _logger;

        public ServiceEndpointClientLoggingProxy(ILoggerFactory loggerFactory, IServiceEndpointClient endpoint)
        {
            _logger = loggerFactory.Create(endpoint.GetType().FullName);
            _endpoint = endpoint;
        }

        public bool CanHandle(IMessage message)
        {
            return _endpoint.CanHandle(message);
        }

        public IObservable<IEvent> Events
        {
            get
            {
                return _endpoint.Events.Do(
                    ev => _logger.Info(string.Format((string) "Received {0}", (object) ev.ToString())),
                    exception => _logger.Error("Error receiving events", exception),
                    () => _logger.Warn("Events completed"));
            }
        }

        public Task SendAsync(ICommand command)
        {
            _logger.Info(string.Format("Sending {0}", command));
            return _endpoint.SendAsync(command);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _endpoint.GetResponses(request).Do(
                response => _logger.Info(string.Format("Received {0}", response.ToString())),
                exception => _logger.Error("Error receiving responses", exception),
                () => _logger.Warn("Responses completed"));
        }
    }
}