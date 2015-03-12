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
}