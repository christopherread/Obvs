using System;
using Obvs.Logging;
using Obvs.Serialization;

namespace Obvs.Configuration
{
    public interface ICanAddEndpointOrLoggingOrCreateOrCorrelation : ICanCreate, ICanAddEndpoint, ICanSpecifyLogging, ICanSpecifyRequestCorrelationProvider
    {
    }

    public interface ICanCreate
    {
        IServiceBus Create();
        IServiceBusClient CreateClient();
    }

    public interface ICanAddEndpoint
    {
        ICanAddEndpointOrLoggingOrCreateOrCorrelation WithEndpoint(IServiceEndpointClient endpointClient);
        ICanAddEndpointOrLoggingOrCreateOrCorrelation WithEndpoint(IServiceEndpoint endpoint);

        ICanAddEndpointOrLoggingOrCreateOrCorrelation WithEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCreateOrCorrelation WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCreateOrCorrelation WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider);
    }

    public interface ICanSpecifyRequestCorrelationProvider
    {
        ICanAddEndpointOrLoggingOrCreateOrCorrelation WithCorrelationProvider(IRequestCorrelationProvider correlationProvider);
    }

    public interface ICanSpecifyLogging
    {
        ICanCreate UsingLogging(ILoggerFactory loggerFactory, Func<IEndpoint, bool> enableLogging = null);
        ICanCreate UsingDebugLogging(Func<IEndpoint, bool> enableLogging = null);
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
        ICanAddEndpointOrLoggingOrCreateOrCorrelation AsClient();
        ICanAddEndpointOrLoggingOrCreateOrCorrelation AsServer();
        ICanAddEndpointOrLoggingOrCreateOrCorrelation AsClientAndServer();
    }
}