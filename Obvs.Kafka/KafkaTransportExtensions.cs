using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Obvs.Configuration;
using Obvs.Kafka;
using Obvs.Kafka.Configuration;
using Obvs.Types;

// In the builder's namespace, so UseKafka() is found without an extra using
namespace Obvs.Hosting
{
    public static class KafkaTransportExtensions
    {
        /// <summary>
        /// Carries this service's messages over Kafka, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:Kafka")</c>, including security settings for managed
        /// platforms such as Confluent Cloud, Amazon MSK and Azure Event Hubs. The service's topics are
        /// created at startup if they don't exist. The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseKafka(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<KafkaOptions>(service.Name).Bind(configuration);
            return UseKafka(service, ConfigurationKey(configuration));
        }

        /// <summary>
        /// Carries this service's messages over Kafka, with settings bound from <paramref name="configuration"/>, then
        /// changed by <paramref name="configure"/>, for example to set <see cref="KafkaOptions.OAuthBearerTokenProvider"/>,
        /// which can't come from configuration. The service's topics are created at startup if they don't exist.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseKafka(this ObvsServiceBuilder service, IConfiguration configuration, Action<KafkaOptions> configure)
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

            service.Services.AddOptions<KafkaOptions>(service.Name).Bind(configuration).Configure(configure);
            return UseKafka(service, ConfigurationKey(configuration));
        }

        /// <summary>
        /// Carries this service's messages over Kafka, with settings set in code. The service's topics are
        /// created at startup if they don't exist. The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseKafka(this ObvsServiceBuilder service, Action<KafkaOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<KafkaOptions>(service.Name).Configure(configure);
            return UseKafka(service, setting => $"options.{setting} in UseKafka(...)");
        }

        // Where a setting is set, for errors: a configuration key, or a property in code
        private static Func<string, string> ConfigurationKey(IConfiguration configuration)
        {
            var prefix = configuration is IConfigurationSection section ? section.Path + ":" : "";
            return setting => $"configuration key {prefix}{setting}";
        }

        private static ObvsServiceBuilder UseKafka(ObvsServiceBuilder service, Func<string, string> whereToSet)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSet))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)));
        }

        private static KafkaOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, KafkaOptions options, Func<string, string> whereToSet)
        {
            if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            {
                yield return $"Service '{service.Name}' has no Kafka brokers. Set {whereToSet("BootstrapServers")}, for example localhost:9092.";
            }
            foreach (var error in KafkaSecurityChecks.Validate(service.Name, options, whereToSet))
            {
                yield return error;
            }
            if (service.Serializer == null || service.DeserializerFactory == null)
            {
                yield return $"Service '{service.Name}' uses Kafka but has no serializer. Call SerializedWith(...) on it inside AddService(\"{service.Name}\", ...).";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, KafkaOptions options)
        {
            var kafkaConfiguration = new KafkaConfiguration(options);
            KafkaTopics.EnsureCreated(service.Name, kafkaConfiguration, new[]
            {
                $"{service.Name}.Commands", $"{service.Name}.Events", $"{service.Name}.Requests", $"{service.Name}.Responses",
            });

            var providerType = typeof(KafkaServiceEndpointProvider<,,,,,>).MakeGenericType(
                service.ServiceMessageType, typeof(IMessage), typeof(ICommand), typeof(IEvent), typeof(IRequest), typeof(IResponse));

            return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                service.Name, kafkaConfiguration, null, null, service.Serializer, service.DeserializerFactory, null, null, null, null, service.InstanceId);
        }
    }
}
