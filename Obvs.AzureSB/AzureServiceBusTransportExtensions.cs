using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obvs.AzureSB;
using Obvs.Configuration;
using Obvs.Transports;
using Obvs.Types;

// In the builder's namespace, so UseAzureServiceBus() is found without an extra using
namespace Obvs.Hosting
{
    /// <summary>Carries services over Azure Service Bus: <c>service.UseAzureServiceBus(...)</c> inside <c>AddService</c>.</summary>
    public static class AzureServiceBusTransportExtensions
    {
        // Service Bus limits subscription names, and each bus's event subscription is named after its instance ID
        private const int MaxSubscriptionNameLength = 50;

        // The lock durations Service Bus allows on a queue or subscription
        private static readonly TimeSpan MinLockDuration = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxLockDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Carries this service's messages over Azure Service Bus, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:AzureServiceBus")</c>. Commands and requests go to
        /// queues the service's buses share, so each is handled once; events reach every bus. The service's queues and
        /// topic, and this bus's own subscription and reply queue, are created at startup if they don't exist.
        /// </summary>
        public static ObvsServiceBuilder UseAzureServiceBus(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            return UseAzureServiceBus(service, configuration, options => { });
        }

        /// <summary>
        /// Carries this service's messages over Azure Service Bus, with settings bound from <paramref name="configuration"/>,
        /// then changed by <paramref name="configure"/>, for example to set <see cref="AzureServiceBusOptions.Credential"/>,
        /// which can't come from configuration. Commands and requests go to queues the service's buses share, so each is
        /// handled once; events reach every bus. The service's queues and topic, and this bus's own subscription and reply
        /// queue, are created at startup if they don't exist.
        /// </summary>
        public static ObvsServiceBuilder UseAzureServiceBus(this ObvsServiceBuilder service, IConfiguration configuration, Action<AzureServiceBusOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<AzureServiceBusOptions>(service.Name).Bind(configuration).Configure(configure);
            var prefix = configuration is IConfigurationSection section ? section.Path + ":" : "";
            return UseAzureServiceBus(service,
                $"configuration key {prefix}FullyQualifiedNamespace (for example contoso.servicebus.windows.net), or {prefix}ConnectionString");
        }

        /// <summary>
        /// Carries this service's messages over Azure Service Bus, with settings set in code. Commands and requests go to
        /// queues the service's buses share, so each is handled once; events reach every bus. The service's queues and
        /// topic, and this bus's own subscription and reply queue, are created at startup if they don't exist.
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
            return service.UseTransport("Azure Service Bus",
                transport => CreateProvider(transport, GetOptions(transport),
                    transport.ServiceProvider.GetService<IServiceBusAdministrationFactory>(), transport.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Obvs.AzureSB")),
                transport => Validate(transport, GetOptions(transport), whereToSetNamespace));
        }

        private static AzureServiceBusOptions GetOptions(ObvsTransportContext service)
        {
            return service.ServiceProvider.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get(service.ServiceName);
        }

        private static IEnumerable<string> Validate(ObvsTransportContext service, AzureServiceBusOptions options, string whereToSetNamespace)
        {
            var hasNamespace = !string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace);
            var hasConnectionString = !string.IsNullOrWhiteSpace(options.ConnectionString);
            if (!hasNamespace && !hasConnectionString)
            {
                yield return $"Service '{service.ServiceName}' has no Azure Service Bus namespace. Set {whereToSetNamespace}.";
            }
            if (hasNamespace && hasConnectionString)
            {
                yield return $"Service '{service.ServiceName}' sets both an Azure Service Bus namespace and a connection string. Set one, not both: " +
                             "FullyQualifiedNamespace to sign in with a credential, or ConnectionString to use a shared access key.";
            }
            if (options.MaxDeliveryAttempts < 1)
            {
                yield return $"Service '{service.ServiceName}' sets Azure Service Bus MaxDeliveryAttempts to {options.MaxDeliveryAttempts}, but it must be at least 1. " +
                             "Leave it unset for Service Bus's default of 10.";
            }
            if (options.MaxConcurrentMessages < 1)
            {
                yield return $"Service '{service.ServiceName}' sets Azure Service Bus MaxConcurrentMessages to {options.MaxConcurrentMessages}, but it must be at least 1. " +
                             "Leave it unset to take as many at once as the service's handlers' MaxConcurrency add up to.";
            }
            if (options.LockDuration is TimeSpan lockDuration && (lockDuration < MinLockDuration || lockDuration > MaxLockDuration))
            {
                yield return $"Service '{service.ServiceName}' sets Azure Service Bus LockDuration to {lockDuration}, but Service Bus allows between 5 seconds and 5 minutes. " +
                             "Leave it unset for Service Bus's default of 1 minute.";
            }
            if (options.MaxHandlerDuration < TimeSpan.Zero)
            {
                yield return $"Service '{service.ServiceName}' sets Azure Service Bus MaxHandlerDuration to {options.MaxHandlerDuration}, but it can't be negative. " +
                             "Set TimeSpan.Zero to never renew a message's lock.";
            }
            if (service.InstanceId.Length > MaxSubscriptionNameLength)
            {
                yield return $"Service '{service.ServiceName}' uses Azure Service Bus, which names this bus's event subscription after its instance ID " +
                             $"and allows {MaxSubscriptionNameLength} characters, but instance ID '{service.InstanceId}' has {service.InstanceId.Length}. " +
                             "Set a shorter one with WithInstanceId(...).";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsTransportContext service, AzureServiceBusOptions options,
            IServiceBusAdministrationFactory administrationFactory, ILogger logger)
        {
            if (logger != null)
            {
                options.Logger = logger;
            }
            options.ServiceName = service.ServiceName;
            var admin = administrationFactory?.Create(options) ?? new ServiceBusAdministration(options);
            Func<Type, int> concurrency = kind => options.MaxConcurrentMessages ?? service.HandlerConcurrency(kind);
            var providerType = typeof(AzureServiceBusEndpointProvider<>).MakeGenericType(service.ServiceMessageType);
            try
            {
                return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                    service.ServiceName, service.InstanceId, options, concurrency, service.Serializer, service.DeserializerFactory, admin, service.IsClient);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                // Reflection wraps what the provider throws, such as a configuration error found setting up entities
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
