using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.NetMQ.Configuration
{
    public static class NetMqConfigExtensions
    {
        public static ICanAddNetMqServiceName WithNetMqEndpoints<TServiceMessage>(this ICanAddEndpoint canAddEndpoint) where TServiceMessage : IMessage
        {
            return new NetMqFluentConfig<TServiceMessage>(canAddEndpoint);
        }
    }
}