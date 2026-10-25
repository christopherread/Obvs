using System;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Obvs;
using Obvs.Hosting;

// In this namespace, as is conventional for AddX extensions, so they're found without an extra using
namespace Microsoft.Extensions.DependencyInjection
{
    public static class ObvsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the service bus to the container: one singleton, resolvable as both
        /// <see cref="IServiceBus"/> and <see cref="IServiceBusClient"/>, started and stopped
        /// with the host. Calling this more than once adds to the same bus.
        /// </summary>
        public static IServiceCollection AddObvs(this IServiceCollection services, Action<ObvsBuilder> configure)
        {
            return AddObvs(services, key: null, configure);
        }

        /// <summary>
        /// Adds a separate, keyed service bus, resolved with <c>[FromKeyedServices(key)]</c>.
        /// Most apps need only the unkeyed bus, which can host many services.
        /// </summary>
        public static IServiceCollection AddObvs(this IServiceCollection services, string key, Action<ObvsBuilder> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var registrations = GetOrAddRegistrations(services);
            var registrationKey = key ?? DefaultBusKey;
            if (!registrations.TryGetValue(registrationKey, out var builder))
            {
                builder = new ObvsBuilder(services);
                registrations.Add(registrationKey, builder);
                AddBus(services, key, builder);
            }

            configure?.Invoke(builder);
            return services;
        }

        // The singletons are registered once per bus; later AddObvs calls only add services to its builder,
        // which is read when the bus is first resolved
        private static void AddBus(IServiceCollection services, string key, ObvsBuilder builder)
        {
            if (key == null)
            {
                services.AddSingleton<IServiceBus>(serviceProvider => builder.Build(serviceProvider));
                services.AddSingleton<IServiceBusClient>(serviceProvider => serviceProvider.GetRequiredService<IServiceBus>());
            }
            else
            {
                services.AddKeyedSingleton<IServiceBus>(key, (serviceProvider, _) => builder.Build(serviceProvider));
                services.AddKeyedSingleton<IServiceBusClient>(key, (serviceProvider, _) => serviceProvider.GetRequiredKeyedService<IServiceBus>(key));
            }

            // Not AddHostedService, which would de-duplicate the hosted services of keyed buses
            services.AddSingleton<IHostedService>(serviceProvider => new ObvsHostedService(serviceProvider, key, builder));
        }

        private static readonly object DefaultBusKey = new object();

        private static Dictionary<object, ObvsBuilder> GetOrAddRegistrations(IServiceCollection services)
        {
            foreach (var descriptor in services)
            {
                if (descriptor.ServiceType == typeof(ObvsRegistrations) && descriptor.ImplementationInstance is ObvsRegistrations existing)
                {
                    return existing.Builders;
                }
            }

            var registrations = new ObvsRegistrations();
            services.AddSingleton(registrations);
            return registrations.Builders;
        }

        // Kept in the service collection so repeated AddObvs calls find the same builders
        private sealed class ObvsRegistrations
        {
            public Dictionary<object, ObvsBuilder> Builders { get; } = new Dictionary<object, ObvsBuilder>();
        }
    }
}
