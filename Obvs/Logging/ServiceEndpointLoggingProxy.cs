using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Logging
{
    public class ServiceEndpointLoggingProxy : ServiceEndpointLoggingProxy<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpoint
    {
        public ServiceEndpointLoggingProxy(ILoggerFactory loggerFactory, IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> endpoint, Func<Type, LogLevel> logLevelSend, Func<Type, LogLevel> logLevelReceive)
            : base(loggerFactory, endpoint, logLevelSend, logLevelReceive)
        {
        }
    }

    public class ServiceEndpointLoggingProxy<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        private readonly IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> _endpoint;
        private readonly ILogger _logger;
        private readonly LogLevel _logLevelSendEvent;
        private readonly LogLevel _logLevelSendResponse;
        private readonly LogLevel _logLevelReceiveCommand;
        private readonly LogLevel _logLevelReceiveRequest;

        public ServiceEndpointLoggingProxy(ILoggerFactory loggerFactory, IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, Func<Type, LogLevel> logLevelSend, Func<Type, LogLevel> logLevelReceive)
        {
            _logger = loggerFactory.Create(endpoint.Name);
            _endpoint = endpoint;
            _logLevelSendEvent = logLevelSend(typeof(TEvent));
            _logLevelSendResponse = logLevelSend(typeof(TResponse));
            _logLevelReceiveCommand = logLevelReceive(typeof(TCommand));
            _logLevelReceiveRequest = logLevelReceive(typeof(TRequest));

            TryLog(() => _logger.Debug("Created"));
            TryLog(() => _logger.Debug(string.Format("CommandLogLevel={0}, EventLogLevel={1}, RequestLogLevel={2}, ResponseLogLevel={3}", _logLevelReceiveCommand, _logLevelSendEvent, _logLevelReceiveRequest, _logLevelSendResponse)));
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
            get { return _endpoint.Requests.Do(LogRequestReceived, LogRequestsError, LogRequestsCompleted); }
        }

        public IObservable<TCommand> Commands
        {
            get { return _endpoint.Commands.Do(LogCommandReceived, LogCommandsException, LogCommandsCompleted); }
        }

        public Task PublishAsync(TEvent ev)
        {
            LogPublishEvent(ev);
            return _endpoint.PublishAsync(ev);
        }

        public Task ReplyAsync(TRequest request, TResponse response)
        {
            LogReplyRequest(request, response);
            return _endpoint.ReplyAsync(request, response);
        }
        
        public void Dispose()
        {
            TryLog(() => _logger.Debug("Disposing"));
            _endpoint.Dispose();
        }
        
        private void LogPublishEvent(TEvent ev)
        {
            TryLog(() => _logger.Log(_logLevelSendEvent, string.Format("Publishing event {0}", ev)));
        }

        private void LogReplyRequest(TRequest request, TResponse response)
        {
            TryLog(() => _logger.Log(_logLevelSendResponse, string.Format("Replying to {0} with {1}", request, response)));
        }

        private void LogRequestsCompleted()
        {
            TryLog(() => _logger.Warn("Requests completed"));
        }

        private void LogRequestsError(Exception exception)
        {
            TryLog(() => _logger.Error("Error receiving requests", exception));
        }

        private void LogRequestReceived(TRequest request)
        {
            TryLog(() => _logger.Log(_logLevelReceiveRequest, string.Format("Received request {0}", request.ToString())));
        }
        
        private void LogCommandsCompleted()
        {
            TryLog(() => _logger.Warn("Commands completed"));
        }

        private void LogCommandsException(Exception exception)
        {
            TryLog(() => _logger.Error("Error receiving commands", exception));
        }

        private void LogCommandReceived(TCommand command)
        {
            TryLog(() => _logger.Log(_logLevelReceiveCommand, string.Format("Received command {0}", command)));
        }

        private void TryLog(Action log, [CallerMemberName] string caller = null)
        {
            try
            {
                log();
            }
            catch (Exception e)
            {
                Debug.WriteLine("{0}() Error: {1}", caller, e);
            }
        }
    }
}