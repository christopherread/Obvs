using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Logging
{
    public class ServiceEndpointClientLoggingProxy : ServiceEndpointClientLoggingProxy<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceEndpointClient
    {
        public ServiceEndpointClientLoggingProxy(ILoggerFactory loggerFactory, IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse> endpoint, Func<Type, LogLevel> logLevelSend, Func<Type, LogLevel> logLevelReceive)
            : base(loggerFactory, endpoint, logLevelSend, logLevelReceive)
        {
        }
    }

    public class ServiceEndpointClientLoggingProxy<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : TMessage
        where TEvent : TMessage
        where TRequest : TMessage
        where TResponse : TMessage
    {
        private readonly IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> _endpoint;
        private readonly ILogger _logger;
        private readonly LogLevel _logLevelSendCommand;
        private readonly LogLevel _logLevelReceiveEvent;
        private readonly LogLevel _logLevelReceiveResponse;
        private readonly LogLevel _logLevelSendRequest;

        public ServiceEndpointClientLoggingProxy(ILoggerFactory loggerFactory, IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint, Func<Type, LogLevel> logLevelSend, Func<Type, LogLevel> logLevelReceive)
        {
            _logger = loggerFactory.Create(endpoint.Name);
            _endpoint = endpoint;

            _logLevelSendCommand = logLevelSend(typeof(TCommand));
            _logLevelReceiveEvent = logLevelReceive(typeof(TEvent));
            _logLevelSendRequest = logLevelReceive(typeof(TRequest));
            _logLevelReceiveResponse = logLevelReceive(typeof(TResponse));

            TryLog(() => _logger.Debug("Created"));
            TryLog(() => _logger.Debug(string.Format("CommandLogLevel={0}, EventLogLevel={1}, RequestLogLevel={2}, ResponseLogLevel={3}", _logLevelSendCommand, _logLevelReceiveEvent, _logLevelSendRequest, _logLevelReceiveResponse)));
        }

        public bool CanHandle(TMessage message)
        {
            return _endpoint.CanHandle(message);
        }

        public string Name
        {
            get { return _endpoint.Name; }
        }

        public IObservable<TEvent> Events
        {
            get { return _endpoint.Events.Do(LogEventReceived, LogEventsException, LogEventsCompleted); }
        }

        public Task SendAsync(TCommand command)
        {
            LogSendCommand(command);
            return _endpoint.SendAsync(command);
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            LogRequestReceived(request);
            return _endpoint.GetResponses(request).Do(LogResponseReceived, LogResponsesException, LogResponsesCompleted);
        }

        public void Dispose()
        {
            TryLog(() => _logger.Debug("Disposing"));
            _endpoint.Dispose();
        }

        private void LogRequestReceived(TRequest request)
        {
            TryLog(() => _logger.Log(_logLevelSendRequest, string.Format("Sending request {0}", request)));
        }

        private void LogSendCommand(TCommand command)
        {
            TryLog(() => _logger.Log(_logLevelSendCommand, string.Format("Sending command {0}", command)));
        }

        private void LogResponsesCompleted()
        {
            TryLog(() => _logger.Warn("Responses completed"));
        }

        private void LogEventsCompleted()
        {
            TryLog(() => _logger.Warn("Events completed"));
        }

        private void LogEventsException(Exception exception)
        {
            TryLog(() => _logger.Error("Error receiving events", exception));
        }

        private void LogEventReceived(TEvent ev)
        {
            TryLog(() => _logger.Log(_logLevelReceiveEvent, string.Format("Received event {0}", ev)));
        }

        private void LogResponseReceived(TResponse response)
        {
            TryLog(() => _logger.Log(_logLevelReceiveResponse, string.Format("Received response {0}", response)));
        }

        private void LogResponsesException(Exception exception)
        {
            TryLog(() => _logger.Error("Error receiving responses", exception));
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