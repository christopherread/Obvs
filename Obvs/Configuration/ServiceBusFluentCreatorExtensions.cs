using Obvs.Types;

// v6's fluent configuration, kept for v6 apps and obsolete in v7: its public members are exempt from the rule that every one is documented
#pragma warning disable CS1570, CS1572, CS1591

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