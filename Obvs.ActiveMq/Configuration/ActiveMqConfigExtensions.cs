using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.ActiveMq.Configuration
{
    public static class ActiveMqConfigExtensions
    {
        public static ICanSpecifyActiveMqServiceName WithActiveMqEndpoints<TServiceMessage>(this ICanAddEndpoint canAddEndpoint) where TServiceMessage : IMessage
        {
            return new ActiveMqFluentConfig<TServiceMessage>(canAddEndpoint);
        }
    }
}