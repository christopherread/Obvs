using System;
using System.Reactive.Concurrency;
using System.Reflection;
using Obvs.Logging;
using Obvs.MessageProperties;
using Obvs.Monitoring;
using Obvs.Serialization;

namespace Obvs.Configuration
{
    public interface ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> :
        ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyRequestCorrelationProvider<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyLocalBus<TMessage, TCommand, TEvent, TRequest, TResponse>,
        ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
    }

    public interface ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> CreateServiceBus();
        IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> CreateServiceBusClient();
    }

    public interface ICanAddEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithEndpoint(IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpointClient);
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithEndpoint(IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse> endpoint);

        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithEndpoints(IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithClientEndpoints(IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> WithServerEndpoints(IServiceEndpointProvider<TMessage, TCommand, TEvent, TRequest, TResponse> serviceEndpointProvider);
    }

    public interface ICanSpecifyRequestCorrelationProvider<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> CorrelatesRequestWith(IRequestCorrelationProvider<TRequest, TResponse> correlationProvider);
    }

   public interface ICanSpecifyLogging<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingLogging(ILoggerFactory loggerFactory, Func<IEndpoint<TMessage>, bool> enableLogging = null, Func<Type, LogLevel> logLevelSend = null, Func<Type, LogLevel> logLevelReceive = null);
        ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingDebugLogging(Func<IEndpoint<TMessage>, bool> enableLogging = null, Func<Type, LogLevel> logLevelSend = null, Func<Type, LogLevel> logLevelReceive = null);
        ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingConsoleLogging(Func<IEndpoint<TMessage>, bool> enableLogging = null, Func<Type, LogLevel> logLevelSend = null, Func<Type, LogLevel> logLevelReceive = null);
    }

   public interface ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> : 
       ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse>,
       ICanSpecifyLogging<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
    }

   public interface ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> : 
       ICanCreate<TMessage, TCommand, TEvent, TRequest, TResponse>,
       ICanSpecifyLogging<TMessage, TCommand, TEvent, TRequest, TResponse>,
       ICanSpecifyMonitoring<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
    }

    public interface ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
       where TMessage : class
       where TCommand : class, TMessage
       where TEvent : class, TMessage
       where TRequest : class, TMessage
       where TResponse : class, TMessage
    {
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory);
    }

    public interface ICanFilterEndpointMessageTypeAssemblies<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> FilterMessageTypeAssemblies(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null);
    }

    public interface ICanCreateEndpointAsClientOrServer<TMessage, TCommand, TEvent, TRequest, TResponse> : ICanFilterEndpointMessageTypeAssemblies<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClient();
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsServer();
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AsClientAndServer();
    }

    public interface ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> : ICanSpecifyEndpointSerializers<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        ICanSpecifyPropertyProviders<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMessagePropertyProviderFor<T>(IMessagePropertyProvider<T> provider) where T : class, TMessage;
    }

    public interface ICanSpecifyLocalBus<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>
        /// Messages sent to this ServiceBus will also be received by local subscribers
        /// </summary>
        /// <param name="localBus">Optionally allows your own local bus instance to be used</param>
        /// <returns></returns>
        ICanSpecifyLocalBusOptions<TMessage, TCommand, TEvent, TRequest, TResponse> PublishLocally(IMessageBus<TMessage> localBus = null);
    }

    public interface ICanSpecifyLocalBusOptions<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        /// <summary>
        /// Configures message types for which this process is configured as a server to also be sent and received by local subscribers
        /// </summary>
        ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> AnyMessagesWithNoEndpointClients();

        /// <summary>
        /// Configures message types which have no endpoints configured to be sent and received by local subscribers
        /// </summary>
        ICanSpecifyLoggingOrMonitoringOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> OnlyMessagesWithNoEndpoints();
    }

    public interface ICanSpecifyMonitoring<TMessage, TCommand, TEvent, TRequest, TResponse> 
        where TResponse : class, TMessage 
        where TRequest : class, TMessage 
        where TEvent : class, TMessage 
        where TCommand : class, TMessage 
        where TMessage : class
    {
        ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingMonitor(IMonitorFactory<TMessage> monitorFactory);
        ICanSpecifyLoggingOrCreate<TMessage, TCommand, TEvent, TRequest, TResponse> UsingConsoleMonitor(TimeSpan period, IScheduler scheduler = null);
    }
}