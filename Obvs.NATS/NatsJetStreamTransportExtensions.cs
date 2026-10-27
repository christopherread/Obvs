using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obvs.Configuration;
using Obvs.NATS;
using Obvs.NATS.JetStream;
using Obvs.Types;

// In the builder's namespace, so UseNatsJetStream() is found without an extra using
namespace Obvs.Hosting
{
    public static class NatsJetStreamTransportExtensions
    {
        // Characters JetStream doesn't allow in stream and consumer names, which Obvs makes from the service's name and
        // the bus's instance ID
        private static readonly char[] InvalidNameCharacters = { '.', '*', '>', '/', '\\' };
        private const string InvalidNameCharactersDescription = "whitespace, '.', '*', '>', '/' or '\\'";

        /// <summary>
        /// Carries this service's messages over NATS JetStream, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:NatsJetStream")</c>. Messages are kept in streams
        /// until handled: commands and requests through consumers the service's buses share, so each is handled once;
        /// events reach every bus. The service's streams, and this bus's own consumers, are created at startup if they
        /// don't exist. The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseNatsJetStream(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<NatsJetStreamOptions>(service.Name).Bind(configuration);
            var urlKey = configuration is IConfigurationSection section ? section.Path + ":Url" : "Url";
            return UseNatsJetStream(service, $"configuration key {urlKey}");
        }

        /// <summary>
        /// Carries this service's messages over NATS JetStream, with settings set in code. Messages are kept in streams
        /// until handled: commands and requests through consumers the service's buses share, so each is handled once;
        /// events reach every bus. The service's streams, and this bus's own consumers, are created at startup if they
        /// don't exist. The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseNatsJetStream(this ObvsServiceBuilder service, Action<NatsJetStreamOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<NatsJetStreamOptions>(service.Name).Configure(configure);
            return UseNatsJetStream(service, "options.Url in UseNatsJetStream(...)");
        }

        private static ObvsServiceBuilder UseNatsJetStream(ObvsServiceBuilder service, string whereToSetUrl)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetUrl))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service),
                    serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("Obvs.NATS.JetStream")));
        }

        private static NatsJetStreamOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<NatsJetStreamOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, NatsJetStreamOptions options, string whereToSetUrl)
        {
            if (string.IsNullOrWhiteSpace(options.Url))
            {
                yield return $"Service '{service.Name}' has no NATS server address. Set {whereToSetUrl}, for example nats://localhost:4222.";
            }
            if (options.MaxDeliveryAttempts < 1)
            {
                yield return $"Service '{service.Name}' sets NATS JetStream MaxDeliveryAttempts to {options.MaxDeliveryAttempts}, but it must be at least 1. " +
                             "Leave it unset for the default of 10.";
            }
            if (options.MaxConcurrentMessages < 1)
            {
                yield return $"Service '{service.Name}' sets NATS JetStream MaxConcurrentMessages to {options.MaxConcurrentMessages}, but it must be at least 1. " +
                             "Leave it unset to handle one message at a time, in order.";
            }
            if (options.AckWait is TimeSpan ackWait && ackWait <= TimeSpan.Zero)
            {
                yield return $"Service '{service.Name}' sets NATS JetStream AckWait to {ackWait}, but it must be positive. " +
                             "Leave it unset for JetStream's default of 30 seconds.";
            }
            if (options.MaxInProgressDuration < TimeSpan.Zero)
            {
                yield return $"Service '{service.Name}' sets NATS JetStream MaxInProgressDuration to {options.MaxInProgressDuration}, but it can't be negative. " +
                             "Set TimeSpan.Zero to never keep a message in progress.";
            }
            if (!IsValidName(service.Name))
            {
                yield return $"Service '{service.Name}' uses NATS JetStream, which names the service's streams after it, but stream names can't contain " +
                             $"{InvalidNameCharactersDescription}. Give the service a name without them.";
            }
            if (!IsValidName(service.InstanceId))
            {
                yield return $"Service '{service.Name}' uses NATS JetStream, which names this bus's own consumers after its instance ID, but instance ID " +
                             $"'{service.InstanceId}' contains {InvalidNameCharactersDescription}, which consumer names can't. Set another with UseInstanceId(...).";
            }
        }

        private static bool IsValidName(string name)
        {
            return name.IndexOfAny(InvalidNameCharacters) < 0 && !name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, NatsJetStreamOptions options, ILogger logger)
        {
            if (logger != null)
            {
                options.Logger = logger;
            }
            var providerType = typeof(JetStreamEndpointProvider<>).MakeGenericType(service.ServiceMessageType);
            try
            {
                return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                    service.Name, service.InstanceId, options, service.Serializer, service.DeserializerFactory, service.IsServer, service.IsClient);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                // Reflection wraps what the provider throws, such as a server without JetStream
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
