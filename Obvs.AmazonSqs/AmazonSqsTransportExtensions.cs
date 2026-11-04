using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Obvs.AmazonSqs;
using Obvs.Configuration;
using Obvs.Transports;
using Obvs.Types;

// In the builder's namespace, so UseAmazonSqs() is found without an extra using
namespace Obvs.Hosting
{
    /// <summary>Carries services over Amazon SQS and SNS: <c>service.UseAmazonSqs(...)</c> inside <c>AddService</c>.</summary>
    public static class AmazonSqsTransportExtensions
    {
        // SQS limits queue names to 80 letters, digits, hyphens and underscores; SNS topic names allow the same characters
        private const int MaxQueueNameLength = 80;
        private static readonly Regex AllowedName = new Regex("^[A-Za-z0-9_-]+$");

        // The visibility timeouts SQS allows
        private static readonly TimeSpan MinVisibilityTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxVisibilityTimeout = TimeSpan.FromHours(12);

        // How many times a bus refreshes when it was last seen, at least, before it counts as abandoned
        private const int MinRefreshesPerWindow = 3;

        /// <summary>
        /// Carries this service's messages over Amazon SQS and SNS, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:AmazonSqs")</c>. Commands and requests go to SQS
        /// queues the service's buses share, so each is handled once; events go to an SNS topic that delivers them to
        /// every bus. The service's queues and topic, and this bus's own queues, are created at startup if they don't exist.
        /// </summary>
        public static ObvsServiceBuilder UseAmazonSqs(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            return UseAmazonSqs(service, configuration, options => { });
        }

        /// <summary>
        /// Carries this service's messages over Amazon SQS and SNS, with settings bound from <paramref name="configuration"/>,
        /// then changed by <paramref name="configure"/>, for example to set <see cref="AmazonSqsOptions.Credentials"/>,
        /// which can't come from configuration. Commands and requests go to SQS queues the service's buses share, so each
        /// is handled once; events go to an SNS topic that delivers them to every bus. The service's queues and topic, and
        /// this bus's own queues, are created at startup if they don't exist.
        /// </summary>
        public static ObvsServiceBuilder UseAmazonSqs(this ObvsServiceBuilder service, IConfiguration configuration, Action<AmazonSqsOptions> configure)
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

            service.Services.AddOptions<AmazonSqsOptions>(service.Name).Bind(configuration).Configure(configure);
            var prefix = configuration is IConfigurationSection section ? section.Path + ":" : "";
            return UseAmazonSqs(service, $"configuration key {prefix}Region (for example eu-west-1)", $"configuration key {prefix}ServiceUrl");
        }

        /// <summary>
        /// Carries this service's messages over Amazon SQS and SNS, with settings set in code. Commands and requests go to
        /// SQS queues the service's buses share, so each is handled once; events go to an SNS topic that delivers them to
        /// every bus. The service's queues and topic, and this bus's own queues, are created at startup if they don't exist.
        /// </summary>
        public static ObvsServiceBuilder UseAmazonSqs(this ObvsServiceBuilder service, Action<AmazonSqsOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<AmazonSqsOptions>(service.Name).Configure(configure);
            return UseAmazonSqs(service, "options.Region (for example eu-west-1) in UseAmazonSqs(...)", "options.ServiceUrl in UseAmazonSqs(...)");
        }

        private static ObvsServiceBuilder UseAmazonSqs(ObvsServiceBuilder service, string whereToSetRegion, string whereToSetServiceUrl)
        {
            return service.UseTransport("Amazon SQS",
                transport => CreateProvider(transport, GetOptions(transport),
                    transport.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Obvs.AmazonSqs")),
                transport => Validate(transport, GetOptions(transport), whereToSetRegion, whereToSetServiceUrl));
        }

        private static AmazonSqsOptions GetOptions(ObvsTransportContext service)
        {
            return service.ServiceProvider.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>().Get(service.ServiceName);
        }

        private static IEnumerable<string> Validate(ObvsTransportContext service, AmazonSqsOptions options, string whereToSetRegion, string whereToSetServiceUrl)
        {
            if (string.IsNullOrWhiteSpace(options.Region))
            {
                yield return $"Service '{service.ServiceName}' has no AWS region for Amazon SQS. Set {whereToSetRegion}.";
            }
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl) && !IsHttpUrl(options.ServiceUrl))
            {
                yield return $"Service '{service.ServiceName}' sets Amazon SQS ServiceUrl to '{options.ServiceUrl}', which isn't an http or https URL. " +
                             $"Set {whereToSetServiceUrl} to one such as http://localhost:4566, or leave it unset to use AWS's own endpoints.";
            }
            if (options.MaxDeliveryAttempts < 1 || options.MaxDeliveryAttempts > 1000)
            {
                yield return $"Service '{service.ServiceName}' sets Amazon SQS MaxDeliveryAttempts to {options.MaxDeliveryAttempts}, but SQS allows between 1 and 1000. " +
                             "Leave it unset for 10.";
            }
            if (options.MaxConcurrentMessages < 1)
            {
                yield return $"Service '{service.ServiceName}' sets Amazon SQS MaxConcurrentMessages to {options.MaxConcurrentMessages}, but it must be at least 1. " +
                             "Leave it unset to take as many at once as the service's handlers' MaxConcurrency add up to.";
            }
            if (options.VisibilityTimeout < MinVisibilityTimeout || options.VisibilityTimeout > MaxVisibilityTimeout)
            {
                yield return $"Service '{service.ServiceName}' sets Amazon SQS VisibilityTimeout to {options.VisibilityTimeout}, but SQS allows between 1 second and 12 hours. " +
                             "Leave it unset for 1 minute.";
            }
            if (options.MaxHandlerDuration < TimeSpan.Zero || options.MaxHandlerDuration > MaxVisibilityTimeout)
            {
                yield return $"Service '{service.ServiceName}' sets Amazon SQS MaxHandlerDuration to {options.MaxHandlerDuration}, but SQS keeps a message hidden " +
                             "for at most 12 hours from when it's received, so it must be between zero and 12 hours. " +
                             "Set it to the longest a handler takes with a message, TimeSpan.Zero to never extend a message's visibility, or leave it unset for 5 minutes.";
            }
            if (options.CleanUpAbandonedQueues)
            {
                if (options.LastSeenInterval <= TimeSpan.Zero)
                {
                    yield return $"Service '{service.ServiceName}' sets Amazon SQS LastSeenInterval to {options.LastSeenInterval}, but it must be positive. " +
                                 "Leave it unset for 5 minutes, or set CleanUpAbandonedQueues to false to turn clean-up off.";
                }
                else if (options.AbandonedAfter.Ticks < options.LastSeenInterval.Ticks * MinRefreshesPerWindow)
                {
                    yield return $"Service '{service.ServiceName}' sets Amazon SQS AbandonedAfter to {options.AbandonedAfter}, which must be at least three times " +
                                 $"LastSeenInterval ({options.LastSeenInterval}), so that a bus missing a refresh or two isn't taken for crashed and has its queues deleted. " +
                                 "Raise AbandonedAfter, or lower LastSeenInterval.";
                }
            }
            foreach (var error in ValidateNames(service))
            {
                yield return error;
            }
        }

        // Uri alone takes "localhost:4566" as a URL whose scheme is localhost
        private static bool IsHttpUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static IEnumerable<string> ValidateNames(ObvsTransportContext service)
        {
            var namesAllowed = true;
            if (!AllowedName.IsMatch(service.ServiceName))
            {
                namesAllowed = false;
                yield return $"Service '{service.ServiceName}' uses Amazon SQS, which names the service's queues and topic after it, but SQS and SNS names allow only " +
                             "letters, digits, hyphens and underscores. Rename the service.";
            }
            if (!AllowedName.IsMatch(service.InstanceId))
            {
                namesAllowed = false;
                yield return $"Service '{service.ServiceName}' uses Amazon SQS, which names this bus's own queues after its instance ID, but instance ID '{service.InstanceId}' " +
                             "has characters SQS doesn't allow: only letters, digits, hyphens and underscores. Set another with UseInstanceId(...).";
            }
            if (!namesAllowed)
            {
                yield break;
            }

            var longest = AmazonSqsEntities.QueueNames(service.ServiceName, service.InstanceId).OrderByDescending(name => name.Length).First();
            if (longest.Length > MaxQueueNameLength)
            {
                yield return $"Service '{service.ServiceName}' uses Amazon SQS, which allows queue names of up to {MaxQueueNameLength} characters, but this bus's queue " +
                             $"'{longest}' has {longest.Length}. Use a shorter service name, or set a shorter instance ID with UseInstanceId(...).";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsTransportContext service, AmazonSqsOptions options, ILogger logger)
        {
            if (logger != null)
            {
                options.Logger = logger;
            }
            options.ServiceName = service.ServiceName;
            Func<Type, int> concurrency = kind => options.MaxConcurrentMessages ?? service.HandlerConcurrency(kind);
            var providerType = typeof(AmazonSqsEndpointProvider<>).MakeGenericType(service.ServiceMessageType);
            try
            {
                return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                    service.ServiceName, service.InstanceId, options, concurrency, service.Serializer, service.DeserializerFactory, service.IsClient);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                // Reflection wraps what the provider throws, such as an error setting up the service's queues
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
