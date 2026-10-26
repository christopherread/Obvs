using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Obvs.AzureSB;
using Obvs.Configuration;
using Obvs.Types;

// In the builder's namespace, so UseAzureServiceBus() is found without an extra using
namespace Obvs.Hosting
{
    public static class AzureServiceBusTransportExtensions
    {
        // Service Bus limits subscription names, and each bus's event subscription is named after its instance ID
        private const int MaxSubscriptionNameLength = 50;

        /// <summary>
        /// Carries this service's messages over Azure Service Bus, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:AzureServiceBus")</c>. Commands and requests go to
        /// queues the service's buses share, so each is handled once; events reach every bus. The service's queues and
        /// topic, and this bus's own subscription and reply queue, are created at startup if they don't exist.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseAzureServiceBus(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<AzureServiceBusOptions>(service.Name).Bind(configuration);
            var prefix = configuration is IConfigurationSection section ? section.Path + ":" : "";
            return UseAzureServiceBus(service,
                $"configuration key {prefix}FullyQualifiedNamespace (for example contoso.servicebus.windows.net), or {prefix}ConnectionString");
        }

        /// <summary>
        /// Carries this service's messages over Azure Service Bus, with settings set in code. Commands and requests go to
        /// queues the service's buses share, so each is handled once; events reach every bus. The service's queues and
        /// topic, and this bus's own subscription and reply queue, are created at startup if they don't exist.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseAzureServiceBus(this ObvsServiceBuilder service, Action<AzureServiceBusOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<AzureServiceBusOptions>(service.Name).Configure(configure);
            return UseAzureServiceBus(service,
                "options.FullyQualifiedNamespace (for example contoso.servicebus.windows.net), or options.ConnectionString, in UseAzureServiceBus(...)");
        }

        private static ObvsServiceBuilder UseAzureServiceBus(ObvsServiceBuilder service, string whereToSetNamespace)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetNamespace))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)));
        }

        private static AzureServiceBusOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, AzureServiceBusOptions options, string whereToSetNamespace)
        {
            var hasNamespace = !string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace);
            var hasConnectionString = !string.IsNullOrWhiteSpace(options.ConnectionString);
            if (!hasNamespace && !hasConnectionString)
            {
                yield return $"Service '{service.Name}' has no Azure Service Bus namespace. Set {whereToSetNamespace}.";
            }
            if (hasNamespace && hasConnectionString)
            {
                yield return $"Service '{service.Name}' sets both an Azure Service Bus namespace and a connection string. Set one, not both: " +
                             "FullyQualifiedNamespace to sign in with a credential, or ConnectionString to use a shared access key.";
            }
            if (service.Serializer == null || service.DeserializerFactory == null)
            {
                yield return $"Service '{service.Name}' uses Azure Service Bus but has no serializer. Call SerializedWith(...) on it inside AddService(\"{service.Name}\", ...).";
            }
            if (options.MaxDeliveryAttempts < 1)
            {
                yield return $"Service '{service.Name}' sets Azure Service Bus MaxDeliveryAttempts to {options.MaxDeliveryAttempts}, but it must be at least 1. " +
                             "Leave it unset for Service Bus's default of 10.";
            }
            if (service.InstanceId.Length > MaxSubscriptionNameLength)
            {
                yield return $"Service '{service.Name}' uses Azure Service Bus, which names this bus's event subscription after its instance ID " +
                             $"and allows {MaxSubscriptionNameLength} characters, but instance ID '{service.InstanceId}' has {service.InstanceId.Length}. " +
                             "Set a shorter one with UseInstanceId(...).";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, AzureServiceBusOptions options)
        {
            var providerType = typeof(AzureServiceBusEndpointProvider<>).MakeGenericType(service.ServiceMessageType);
            return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                service.Name, service.InstanceId, options, service.Serializer, service.DeserializerFactory);
        }
    }
}
