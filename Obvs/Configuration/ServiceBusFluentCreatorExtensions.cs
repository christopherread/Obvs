using Obvs.Types;

namespace Obvs.Configuration
{
    public static class ServiceBusFluentCreatorExtensions
    {
        public static IServiceBus Create(this ICanCreate<IMessage, ICommand, IEvent, IRequest, IResponse> creator)
        {
            return new ServiceBus(creator.CreateServiceBus());
        }

        public static IServiceBusClient CreateClient(this ICanCreate<IMessage, ICommand, IEvent, IRequest, IResponse> creator)
        {
            return new ServiceBusClient(creator.CreateServiceBusClient());
        }
    }
}