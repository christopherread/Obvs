using System;
using Obvs.Logging;
using Obvs.Serialization;

namespace Obvs.Configuration
{
    public interface ICanAddEndpointOrLoggingOrCorrelationOrCreate : ICanCreate, ICanAddEndpoint, ICanSpecifyLogging, ICanSpecifyRequestCorrelationProvider
    {
    }

    public interface ICanCreate
    {
        IServiceBus Create();
        IServiceBusClient CreateClient();
    }

    public interface ICanAddEndpoint
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate WithEndpoint(IServiceEndpointClient endpointClient);
        ICanAddEndpointOrLoggingOrCorrelationOrCreate WithEndpoint(IServiceEndpoint endpoint);

        ICanAddEndpointOrLoggingOrCorrelationOrCreate WithEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCorrelationOrCreate WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCorrelationOrCreate WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider);
    }

    public interface ICanSpecifyRequestCorrelationProvider
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate CorrelatesRequestWith(IRequestCorrelationProvider correlationProvider);
    }

    public interface ICanSpecifyLogging
    {
        ICanCreate UsingLogging(ILoggerFactory loggerFactory, Func<IEndpoint, bool> enableLogging = null);
        ICanCreate UsingDebugLogging(Func<IEndpoint, bool> enableLogging = null);
        ICanCreate UsingConsoleLogging(Func<IEndpoint, bool> enableLogging = null);
    }

    public interface ICanSpecifyEndpointSerializers
    {
        ICanCreateEndpointAsClientOrServer SerializedWith(IMessageSerializer serializer, IMessageDeserializerFactory deserializerFactory);
    }

    public interface ICanFilterEndpointMessageTypeAssemblies
    {
        ICanCreateEndpointAsClientOrServer FilterMessageTypeAssemblies(string assemblyNameContains);
    }

    public interface ICanCreateEndpointAsClientOrServer : ICanFilterEndpointMessageTypeAssemblies
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate AsClient();
        ICanAddEndpointOrLoggingOrCorrelationOrCreate AsServer();
        ICanAddEndpointOrLoggingOrCorrelationOrCreate AsClientAndServer();
    }
}