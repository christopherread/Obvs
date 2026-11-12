using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Obvs.Testing;

// In the builder's namespace, so UseInMemory() is found without an extra using
namespace Obvs.Hosting
{
    /// <summary>Carries services over an in-memory broker, for tests: <c>service.UseInMemory()</c> inside <c>AddService</c>.</summary>
    public static class InMemoryTransportExtensions
    {
        /// <summary>
        /// Carries this service's messages in memory, on a broker shared by everything in the same container.
        /// Delivery behaves like a real transport: per-service destinations, request/response correlation,
        /// and asynchronous, in-order delivery. Messages are passed by reference, not serialized.
        /// </summary>
        public static ObvsServiceBuilder UseInMemory(this ObvsServiceBuilder service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            // TryAdd, so a broker registered first (as ObvsTestHarness does) is used instead
            service.Services.TryAddSingleton(_ => new InMemoryBroker());
            return service.UseTransport("InMemory", transport =>
                new InMemoryEndpointProvider(transport.ServiceName, transport.ServiceMessageType, transport.ServiceProvider.GetRequiredService<InMemoryBroker>()));
        }

        /// <summary>
        /// Carries this service's messages on <paramref name="broker"/>. Use one broker in several hosts
        /// to test services that run in separate processes in production.
        /// </summary>
        public static ObvsServiceBuilder UseInMemory(this ObvsServiceBuilder service, InMemoryBroker broker)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (broker == null)
            {
                throw new ArgumentNullException(nameof(broker));
            }

            service.Services.TryAddSingleton(broker);
            return service.UseTransport("InMemory", transport => new InMemoryEndpointProvider(transport.ServiceName, transport.ServiceMessageType, broker));
        }
    }
}
