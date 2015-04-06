using System;
using Obvs.Logging;
using Obvs.Serialization;

namespace Obvs.Configuration
{
    public interface ICanAddEndpointOrLoggingOrCreate : ICanCreate, ICanAddEndpoint, ICanSpecifyLogging
    {
    }

    public interface ICanCreate
    {
        IServiceBus Create();
        IServiceBusClient CreateClient();
    }

    public interface ICanAddEndpoint
    {
        ICanAddEndpointOrLoggingOrCreate WithEndpoint(IServiceEndpointClient endpointClient);
        ICanAddEndpointOrLoggingOrCreate WithEndpoint(IServiceEndpoint endpoint);

        ICanAddEndpointOrLoggingOrCreate WithEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCreate WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrLoggingOrCreate WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider);
    }

    public interface ICanSpecifyLogging
    {
        ICanCreate UsingLogging(ILoggerFactory loggerFactory, Func<IEndpoint, bool> enableLogging = null);
        ICanCreate UsingDebugLogging();
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
        ICanAddEndpointOrLoggingOrCreate AsClient();
        ICanAddEndpointOrLoggingOrCreate AsServer();
        ICanAddEndpointOrLoggingOrCreate AsClientAndServer();
    }
}