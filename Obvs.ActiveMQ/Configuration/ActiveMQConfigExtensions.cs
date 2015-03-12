using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMQ.Configuration
{
    public static class ActiveMQConfigExtensions
    {
        public static ICanSpecifyActiveMQServiceName WithActiveMQEndpoints<TServiceMessage>(this ICanAddEndpoint canAddEndpoint) where TServiceMessage : IMessage
        {
            return new ActiveMQFluentConfig<TServiceMessage>(canAddEndpoint);
        }
    }
}