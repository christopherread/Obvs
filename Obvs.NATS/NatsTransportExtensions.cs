using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Obvs.Configuration;
using Obvs.NATS;
using Obvs.NATS.Configuration;
using Obvs.Transports;
using Obvs.Types;

// In the builder's namespace, so UseNats() is found without an extra using
namespace Obvs.Hosting
{
    /// <summary>Carries services over core NATS: <c>service.UseNats(...)</c> inside <c>AddService</c>.</summary>
    public static class NatsTransportExtensions
    {
        /// <summary>
        /// Carries this service's messages over NATS, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:Nats")</c>. Each service on each bus gets
        /// its own connection.
        /// </summary>
        public static ObvsServiceBuilder UseNats(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<NatsOptions>(service.Name).Bind(configuration);
            var urlKey = configuration is IConfigurationSection section ? section.Path + ":Url" : "Url";
            return UseNats(service, $"configuration key {urlKey}");
        }

        /// <summary>
        /// Carries this service's messages over NATS, with settings set in code.
        /// </summary>
        public static ObvsServiceBuilder UseNats(this ObvsServiceBuilder service, Action<NatsOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<NatsOptions>(service.Name).Configure(configure);
            return UseNats(service, "options.Url in UseNats(...)");
        }

        private static ObvsServiceBuilder UseNats(ObvsServiceBuilder service, string whereToSetUrl)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetUrl))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)), "NATS");
        }

        private static NatsOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<NatsOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, NatsOptions options, string whereToSetUrl)
        {
            if (string.IsNullOrWhiteSpace(options.Url))
            {
                yield return $"Service '{service.Name}' has no NATS server address. Set {whereToSetUrl}, for example nats://localhost:4222.";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time.
        // Connections aren't shared: v6's shared connections are process-wide, so one host stopping would
        // close another's.
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, NatsOptions options)
        {
            var settings = new NatsEndpointSettings<IMessage> { ServiceName = service.Name };
            settings.Configure((NatsEndpointSettings<IMessage>.BrokerConnection connection) =>
            {
                connection.Url = options.Url;
                connection.IsShared = false;
            });

            var providerType = typeof(NatsServiceEndpointProvider<,,,,,>).MakeGenericType(
                service.ServiceMessageType, typeof(IMessage), typeof(ICommand), typeof(IEvent), typeof(IRequest), typeof(IResponse));

            return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                settings, service.Serializer, service.DeserializerFactory, null, null, null, null);
        }
    }
}
