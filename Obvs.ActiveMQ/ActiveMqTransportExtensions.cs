using System;
using System.Collections.Generic;
using Apache.NMS.ActiveMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Obvs.ActiveMQ;
using Obvs.ActiveMQ.Configuration;
using Obvs.Configuration;
using Obvs.Types;
using AcknowledgementMode = Obvs.ActiveMQ.AcknowledgementMode;

// In the builder's namespace, so UseActiveMq() is found without an extra using
namespace Obvs.Hosting
{
    public static class ActiveMqTransportExtensions
    {
        /// <summary>
        /// Carries this service's messages over ActiveMQ topics, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:ActiveMq")</c>.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseActiveMq(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<ActiveMqOptions>(service.Name).Bind(configuration);
            var brokerUriKey = configuration is IConfigurationSection section ? section.Path + ":BrokerUri" : "BrokerUri";
            return UseActiveMq(service, $"configuration key {brokerUriKey}");
        }

        /// <summary>
        /// Carries this service's messages over ActiveMQ topics, with settings set in code.
        /// The service also needs a serializer: call <c>SerializedWith(...)</c>.
        /// </summary>
        public static ObvsServiceBuilder UseActiveMq(this ObvsServiceBuilder service, Action<ActiveMqOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<ActiveMqOptions>(service.Name).Configure(configure);
            return UseActiveMq(service, "options.BrokerUri in UseActiveMq(...)");
        }

        private static ObvsServiceBuilder UseActiveMq(ObvsServiceBuilder service, string whereToSetBrokerUri)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetBrokerUri))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)), "ActiveMQ");
        }

        private static ActiveMqOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<ActiveMqOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, ActiveMqOptions options, string whereToSetBrokerUri)
        {
            if (string.IsNullOrWhiteSpace(options.BrokerUri))
            {
                yield return $"Service '{service.Name}' has no ActiveMQ broker. Set {whereToSetBrokerUri}, for example activemq:tcp://localhost:61616.";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time.
        // Client IDs get the bus's instance ID, as on .NET Framework they're otherwise the same for every bus
        // in the process, and the broker refuses a second connection with the same client ID.
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, ActiveMqOptions options)
        {
            var providerType = typeof(ActiveMQServiceEndpointProvider<,,,,,>).MakeGenericType(
                service.ServiceMessageType, typeof(IMessage), typeof(ICommand), typeof(IEvent), typeof(IRequest), typeof(IResponse));
            Action<ConnectionFactory> configureConnectionFactory = factory => factory.ClientId = $"{factory.ClientId}-{service.InstanceId}";

            return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                service.Name, options.BrokerUri, service.Serializer, service.DeserializerFactory, new List<Tuple<Type, AcknowledgementMode>>(),
                null, null, null, null, null, null, options.UserName, options.Password, configureConnectionFactory, false);
        }
    }
}
