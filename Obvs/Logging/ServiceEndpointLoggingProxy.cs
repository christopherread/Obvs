using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Logging
{
    public class ServiceEndpointLoggingProxy : IServiceEndpoint
    {
        private readonly IServiceEndpoint _endpoint;
        private readonly ILogger _logger;

        public ServiceEndpointLoggingProxy(ILoggerFactory loggerFactory, IServiceEndpoint endpoint)
        {
            _logger = loggerFactory.Create(endpoint.GetType().FullName);
            _endpoint = endpoint;
        }

        public bool CanHandle(IMessage message)
        {
            return _endpoint.CanHandle(message);
        }

        public IObservable<IRequest> Requests
        {
            get
            {
                return _endpoint.Requests.Do(
                    request => _logger.Info(string.Format("Received {0}", request.ToString())),
                    exception => _logger.Error("Error receiving requests", exception),
                    () => _logger.Warn("Requests completed"));
            }
        }

        public IObservable<ICommand> Commands
        {
            get
            {
                return _endpoint.Commands.Do(
                    command => _logger.Info(string.Format("Received {0}", command.ToString())),
                    exception => _logger.Error("Error receiving commands", exception),
                    () => _logger.Warn("Commands completed"));
            }
        }

        public Task PublishAsync(IEvent ev)
        {
            _logger.Info(string.Format("Publishing {0}", ev));
            return _endpoint.PublishAsync(ev);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            _logger.Info(string.Format("Replying to {0} with {1}", request, response));
            return _endpoint.ReplyAsync(request, response);
        }
    }
}