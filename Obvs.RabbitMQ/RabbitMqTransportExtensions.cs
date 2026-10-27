using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Obvs.Configuration;
using Obvs.RabbitMQ;
using Obvs.RabbitMQ.Configuration;
using Obvs.Types;

// In the builder's namespace, so UseRabbitMq() is found without an extra using
namespace Obvs.Hosting
{
    public static class RabbitMqTransportExtensions
    {
        /// <summary>
        /// Carries this service's messages over RabbitMQ, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:RabbitMq")</c>.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseRabbitMq(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<RabbitMqOptions>(service.Name).Bind(configuration);
            var uriKey = configuration is IConfigurationSection section ? section.Path + ":Uri" : "Uri";
            return UseRabbitMq(service, $"configuration key {uriKey}");
        }

        /// <summary>
        /// Carries this service's messages over RabbitMQ, with settings set in code.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseRabbitMq(this ObvsServiceBuilder service, Action<RabbitMqOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<RabbitMqOptions>(service.Name).Configure(configure);
            return UseRabbitMq(service, "options.Uri in UseRabbitMq(...)");
        }

        private static ObvsServiceBuilder UseRabbitMq(ObvsServiceBuilder service, string whereToSetUri)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetUri))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)));
        }

        private static RabbitMqOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, RabbitMqOptions options, string whereToSetUri)
        {
            if (options.Uri == null)
            {
                yield return $"Service '{service.Name}' has no RabbitMQ broker address. Set {whereToSetUri}, for example amqp://localhost:5672.";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, RabbitMqOptions options)
        {
            var providerType = typeof(RabbitMQServiceEndpointProvider<,,,,,>).MakeGenericType(
                service.ServiceMessageType, typeof(IMessage), typeof(ICommand), typeof(IEvent), typeof(IRequest), typeof(IResponse));

            return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                service.Name, options.Uri.ToString(), service.Serializer, service.DeserializerFactory,
                null, null, options.AutomaticRecoveryEnabled, service.InstanceId);
        }
    }
}
