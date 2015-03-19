using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.RabbitMQ.Configuration
{
    public static class RabbitMQConfigExtensions
    {
        public static ICanSpecifyRabbitMQServiceName WithRabbitMQEndpoints<TServiceMessage>(this ICanAddEndpoint canAddEndpoint) where TServiceMessage : IMessage
        {
            return new RabbitMQFluentConfig<TServiceMessage>(canAddEndpoint);
        }
    }
}