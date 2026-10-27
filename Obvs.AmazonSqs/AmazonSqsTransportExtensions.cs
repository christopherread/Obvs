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
using Obvs.Types;

// In the builder's namespace, so UseAmazonSqs() is found without an extra using
namespace Obvs.Hosting
{
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
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<AmazonSqsOptions>(service.Name).Bind(configuration);
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
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetRegion, whereToSetServiceUrl))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service),
                    serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("Obvs.AmazonSqs")), "Amazon SQS");
        }

        private static AmazonSqsOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<AmazonSqsOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, AmazonSqsOptions options, string whereToSetRegion, string whereToSetServiceUrl)
        {
            if (string.IsNullOrWhiteSpace(options.Region))
            {
                yield return $"Service '{service.Name}' has no AWS region for Amazon SQS. Set {whereToSetRegion}.";
            }
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl) && !IsHttpUrl(options.ServiceUrl))
            {
                yield return $"Service '{service.Name}' sets Amazon SQS ServiceUrl to '{options.ServiceUrl}', which isn't an http or https URL. " +
                             $"Set {whereToSetServiceUrl} to one such as http://localhost:4566, or leave it unset to use AWS's own endpoints.";
            }
            if (options.MaxDeliveryAttempts < 1 || options.MaxDeliveryAttempts > 1000)
            {
                yield return $"Service '{service.Name}' sets Amazon SQS MaxDeliveryAttempts to {options.MaxDeliveryAttempts}, but SQS allows between 1 and 1000. " +
                             "Leave it unset for 10.";
            }
            if (options.MaxConcurrentCalls < 1)
            {
                yield return $"Service '{service.Name}' sets Amazon SQS MaxConcurrentCalls to {options.MaxConcurrentCalls}, but it must be at least 1. " +
                             "Leave it unset to handle one message at a time.";
            }
            if (options.VisibilityTimeout < MinVisibilityTimeout || options.VisibilityTimeout > MaxVisibilityTimeout)
            {
                yield return $"Service '{service.Name}' sets Amazon SQS VisibilityTimeout to {options.VisibilityTimeout}, but SQS allows between 1 second and 12 hours. " +
                             "Leave it unset for 1 minute.";
            }
            else if (options.MaxHandlerDuration > TimeSpan.Zero && options.VisibilityTimeout <= options.MaxHandlerDuration)
            {
                yield return $"Service '{service.Name}' sets Amazon SQS VisibilityTimeout to {options.VisibilityTimeout}, which must exceed MaxHandlerDuration ({options.MaxHandlerDuration}): " +
                             "SQS delivers a message again once its visibility timeout passes, even while a handler is still running it. " +
                             "Raise VisibilityTimeout, or lower MaxHandlerDuration if handlers finish sooner.";
            }
            if (options.MaxHandlerDuration <= TimeSpan.Zero)
            {
                yield return $"Service '{service.Name}' sets Amazon SQS MaxHandlerDuration to {options.MaxHandlerDuration}, but it must be positive. " +
                             "Set it to the longest a handler takes with a message, or leave it unset for 30 seconds.";
            }
            if (options.CleanUpAbandonedQueues)
            {
                if (options.LastSeenInterval <= TimeSpan.Zero)
                {
                    yield return $"Service '{service.Name}' sets Amazon SQS LastSeenInterval to {options.LastSeenInterval}, but it must be positive. " +
                                 "Leave it unset for 5 minutes, or set CleanUpAbandonedQueues to false to turn clean-up off.";
                }
                else if (options.AbandonedAfter.Ticks < options.LastSeenInterval.Ticks * MinRefreshesPerWindow)
                {
                    yield return $"Service '{service.Name}' sets Amazon SQS AbandonedAfter to {options.AbandonedAfter}, which must be at least three times " +
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

        private static IEnumerable<string> ValidateNames(ObvsServiceBuilder service)
        {
            var namesAllowed = true;
            if (!AllowedName.IsMatch(service.Name))
            {
                namesAllowed = false;
                yield return $"Service '{service.Name}' uses Amazon SQS, which names the service's queues and topic after it, but SQS and SNS names allow only " +
                             "letters, digits, hyphens and underscores. Rename the service.";
            }
            if (!AllowedName.IsMatch(service.InstanceId))
            {
                namesAllowed = false;
                yield return $"Service '{service.Name}' uses Amazon SQS, which names this bus's own queues after its instance ID, but instance ID '{service.InstanceId}' " +
                             "has characters SQS doesn't allow: only letters, digits, hyphens and underscores. Set another with UseInstanceId(...).";
            }
            if (!namesAllowed)
            {
                yield break;
            }

            var longest = AmazonSqsEntities.QueueNames(service.Name, service.InstanceId).OrderByDescending(name => name.Length).First();
            if (longest.Length > MaxQueueNameLength)
            {
                yield return $"Service '{service.Name}' uses Amazon SQS, which allows queue names of up to {MaxQueueNameLength} characters, but this bus's queue " +
                             $"'{longest}' has {longest.Length}. Use a shorter service name, or set a shorter instance ID with UseInstanceId(...).";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, AmazonSqsOptions options, ILogger logger)
        {
            if (logger != null)
            {
                options.Logger = logger;
            }
            options.ServiceName = service.Name;
            var providerType = typeof(AmazonSqsEndpointProvider<>).MakeGenericType(service.ServiceMessageType);
            try
            {
                return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                    service.Name, service.InstanceId, options, service.Serializer, service.DeserializerFactory, service.IsClient);
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
