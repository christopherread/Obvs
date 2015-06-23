using System;
using System.Collections.Generic;
using System.Linq;
using Obvs.Logging;
using Obvs.Types;

namespace Obvs.Configuration
{
    public class ServiceBusFluentCreator<TMessage, TCommand, TEvent, TRequest, TResponse> : ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>
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
            return new ServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse>(GetEndpointClients(), GetEndpoints(), _requestCorrelationProvider);
        }

        public IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateServiceBusClient()
        {
            return new ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>(GetEndpointClients(), _requestCorrelationProvider);
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
                throw new ArgumentNullException("requestCorrelationProvider");
            }
            
            _requestCorrelationProvider = requestCorrelationProvider;
            return this;
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> GetEndpointClients()
        {
            return _loggerFactory == null ? _endpointClients : _endpointClients.Where(ep => _enableLogging(ep)).Select(ep => ep.CreateLoggingProxy(_loggerFactory, _logLevelSend, _logLevelReceive));
        }

        private IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> GetEndpoints()
        {
            return _loggerFactory == null ? _endpoints : _endpoints.Where(ep => _enableLogging(ep)).Select(ep => ep.CreateLoggingProxy(_loggerFactory, _logLevelSend, _logLevelReceive));
        }
    }

    public static class ServiceBusFluentCreatorExtensions
    {
        public static IServiceBus Create(this ICanCreate<IMessage, ICommand, IEvent, IRequest, IResponse> creator)
        {
            return new ServiceBus(creator.CreateServiceBus());
        }

        public static IServiceBusClient CreateClient(this ICanCreate<IMessage, ICommand, IEvent, IRequest, IResponse> creator)
        {
            return new ServiceBusClient(creator.CreateServiceBusClient());
        }
    }
}