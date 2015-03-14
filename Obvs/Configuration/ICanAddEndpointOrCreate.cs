namespace Obvs.Configuration
{
    public interface ICanAddEndpointOrCreate : ICanCreate, ICanAddEndpoint
    {
    }

    public interface ICanCreate
    {
        IServiceBus Create();
        IServiceBusClient CreateClient();
    }

    public interface ICanAddEndpoint
    {
        ICanAddEndpointOrCreate WithEndpoint(IServiceEndpointClient endpointClient);
        ICanAddEndpointOrCreate WithEndpoint(IServiceEndpoint endpoint);

        ICanAddEndpointOrCreate WithEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrCreate WithClientEndpoints(IServiceEndpointProvider serviceEndpointProvider);
        ICanAddEndpointOrCreate WithServerEndpoints(IServiceEndpointProvider serviceEndpointProvider);
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
        ICanAddEndpointOrCreate AsClient();
        ICanAddEndpointOrCreate AsServer();
        ICanAddEndpointOrCreate AsClientAndServer();
    }
}