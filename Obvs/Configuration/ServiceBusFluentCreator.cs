using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using Obvs.Logging;
using Obvs.Monitoring;

namespace Obvs.Configuration
{
    public enum LocalBusOptions
    {
        MessagesWithNoEndpointClients = 0,
        MessagesWithNoEndpoints = 1,
        AllMessages = 3
    }

    public class ServiceBusFluentCreator<TMessage, TCommand, TEvent, TRequest, TResponse> : 
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>, 
        ICanSpecifyLocalBusOptions<TMessage, TCommand, TEvent, TRequest, TResponse> 
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IList<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpointClients = new List<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>>();
        private readonly IList<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpoints = new List<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>>();
        private ILoggerFactory _loggerFactory;
        private Func<IEndpoint<TMessage>, bool> _enableLogging;
        private IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;
        private Func<Type, LogLevel> _logLevelSend;
        private Func<Type, LogLevel> _logLevelReceive;
        private IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> _localBus;
        private bool _useLocalBus = false;
        private LocalBusOptions _localBusOption = LocalBusOptions.MessagesWithNoEndpointClients;
        private IMonitorFactory<TMessage> _monitorFactory;

        public ServiceBusFluentCreator()
        {
        }

        public ServiceBusFluentCreator(IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
        {
            _requestCorrelationProvider = requestCorrelationProvider;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithEndpoints(IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithClientEndpoints(IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> serviceEndpointProvider)
        {
            _endpointClients.Add(serviceEndpointProvider.CreateEndpointClient());
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithServerEndpoints(IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> serviceEndpointProvider)
        {
            _endpoints.Add(serviceEndpointProvider.CreateEndpoint());
            return this;
        }

        public IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> CreateServiceBus()
        {
            return new ServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse>(
                GetEndpointClients(), 
                GetEndpoints(), 
                _requestCorrelationProvider, 
                _useLocalBus ? (_localBus ?? new SubjectServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse>(_requestCorrelationProvider)) : null, 
                _localBusOption);
        }

        public IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateServiceBusClient()
        {
            return new ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>(
                GetEndpointClients(), 
                GetEndpoints(), 
                _requestCorrelationProvider);
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithEndpoint(IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpointClient)
        {
            _endpointClients.Add(endpointClient);
            return this;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithEndpoint(IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint)
        {
            _endpoints.Add(endpoint);
            return this;
        }

        public ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingLogging(ILoggerFactory loggerFactory, Func<IEndpoint<TMessage>, bool> enableLogging = null, Func<Type, LogLevel> logLevelSend = null, Func<Type, LogLevel> logLevelReceive = null)
        {
            _loggerFactory = loggerFactory;
            _enableLogging = enableLogging ?? (endpoint => true);
            _logLevelSend = logLevelSend ?? (type => LogLevel.Info);
            _logLevelReceive = logLevelReceive ?? (type => LogLevel.Info);
            return this;
        }

        public ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingDebugLogging(Func<IEndpoint<TMessage>, bool> enableLogging = null, Func<Type, LogLevel> logLevelSend = null, Func<Type, LogLevel> logLevelReceive = null)
        {
            return UsingLogging(new DebugLoggerFactory(), enableLogging, logLevelSend, logLevelReceive);
        }

        public ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingConsoleLogging(Func<IEndpoint<TMessage>, bool> enableLogging = null, Func<Type, LogLevel> logLevelSend = null, Func<Type, LogLevel> logLevelReceive = null)
        {
            return UsingLogging(new ConsoleLoggerFactory(), enableLogging, logLevelSend, logLevelReceive);
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> CorrelatesRequestWith(IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
        {
            if (requestCorrelationProvider == null)
            {
                throw new ArgumentNullException(nameof(requestCorrelationProvider));
            }
            
            _requestCorrelationProvider = requestCorrelationProvider;
            return this;
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> GetEndpointClients()
        {
            var endpointsWithLogging = _loggerFactory == null
                ? _endpointClients
                : _endpointClients.Where(ep => _enableLogging(ep))
                    .Select(ep => ep.CreateLoggingProxy(_loggerFactory, _logLevelSend, _logLevelReceive))
                    .Union(_endpointClients.Where(ep => !_enableLogging(ep)));

            var endpointsWithMonitoring = _monitorFactory == null
                ? endpointsWithLogging
                : endpointsWithLogging.Select(ep => ep.CreateMonitoringProxy(_monitorFactory));

            return endpointsWithMonitoring;
        }

        private IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> GetEndpoints()
        {
            var endpointsWithLogging = _loggerFactory == null
                ? _endpoints
                : _endpoints.Where(ep => _enableLogging(ep))
                    .Select(ep => ep.CreateLoggingProxy(_loggerFactory, _logLevelSend, _logLevelReceive))
                    .Union(_endpoints.Where(ep => !_enableLogging(ep)));

            var endpointsWithMonitoring = _monitorFactory == null
                ? endpointsWithLogging
                : endpointsWithLogging.Select(ep => ep.CreateMonitoringProxy(_monitorFactory));

            return endpointsWithMonitoring;
        }

        ICanSpecifyLocalBusOptions<TMessage, TCommand, TEvent, TRequest, TResponse> ICanSpecifyLocalBus<TMessage, TCommand, TEvent, TRequest, TResponse>.PublishLocally(IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> localBus)
        {
            _localBus = localBus;
            _useLocalBus = true;
            return this;
        }

        public ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AnyMessagesWithNoEndpointClients()
        {
            _localBusOption = LocalBusOptions.MessagesWithNoEndpointClients;
            return this;
        }

        public ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> OnlyMessagesWithNoEndpoints()
        {
            _localBusOption = LocalBusOptions.MessagesWithNoEndpoints;
            return this;
        }

        public ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AllMessages()
        {
            _localBusOption = LocalBusOptions.AllMessages;
            return this;
        }

        public ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMonitor(IMonitorFactory<TMessage> monitorFactory)
        {
            _monitorFactory = monitorFactory;
            return this;
        }

        public ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingConsoleMonitor(TimeSpan period, IScheduler scheduler = null)
        {
            _monitorFactory = new ConsoleTimerMonitorFactory<TMessage>(period, scheduler ?? Scheduler.Default);
            return this;
        }
    }

}