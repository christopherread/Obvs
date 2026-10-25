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
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:Kafka")</c>. The service's topics are
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
            var bootstrapServersKey = configuration is IConfigurationSection section ? section.Path + ":BootstrapServers" : "BootstrapServers";
            return UseKafka(service, $"configuration key {bootstrapServersKey}");
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
            return UseKafka(service, "options.BootstrapServers in UseKafka(...)");
        }

        private static ObvsServiceBuilder UseKafka(ObvsServiceBuilder service, string whereToSetBootstrapServers)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetBootstrapServers))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)));
        }

        private static KafkaOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<KafkaOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, KafkaOptions options, string whereToSetBootstrapServers)
        {
            if (string.IsNullOrWhiteSpace(options.BootstrapServers))
            {
                yield return $"Service '{service.Name}' has no Kafka brokers. Set {whereToSetBootstrapServers}, for example localhost:9092.";
            }
            if (service.Serializer == null || service.DeserializerFactory == null)
            {
                yield return $"Service '{service.Name}' uses Kafka but has no serializer. Call SerializedWith(...) on it inside AddService(\"{service.Name}\", ...).";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, KafkaOptions options)
        {
            var kafkaConfiguration = new KafkaConfiguration(options.BootstrapServers);
            KafkaTopics.EnsureCreated(kafkaConfiguration, new[]
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
