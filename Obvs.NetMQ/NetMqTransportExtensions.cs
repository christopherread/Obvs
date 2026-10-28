using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Obvs.Configuration;
using Obvs.NetMQ;
using Obvs.NetMQ.Configuration;
using Obvs.Types;

// In the builder's namespace, so UseNetMq() is found without an extra using
namespace Obvs.Hosting
{
    /// <summary>Carries services over NetMQ (ZeroMQ), with no broker: <c>service.UseNetMq(...)</c> inside <c>AddService</c>.</summary>
    public static class NetMqTransportExtensions
    {
        private const int HighestPort = 65535 - 3;

        /// <summary>
        /// Carries this service's messages over NetMQ, with settings bound from <paramref name="configuration"/>,
        /// for example <c>builder.Configuration.GetSection("Obvs:Orders:NetMq")</c>. The service's server binds its
        /// sockets, and its clients connect to them.
        /// </summary>
        public static ObvsServiceBuilder UseNetMq(this ObvsServiceBuilder service, IConfiguration configuration)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            service.Services.AddOptions<NetMqOptions>(service.Name).Bind(configuration);
            var prefix = configuration is IConfigurationSection section ? section.Path + ":" : "";
            return UseNetMq(service, $"configuration key {prefix}Address", $"configuration key {prefix}Port");
        }

        /// <summary>
        /// Carries this service's messages over NetMQ, with settings set in code.
        /// </summary>
        public static ObvsServiceBuilder UseNetMq(this ObvsServiceBuilder service, Action<NetMqOptions> configure)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            service.Services.AddOptions<NetMqOptions>(service.Name).Configure(configure);
            return UseNetMq(service, "options.Address in UseNetMq(...)", "options.Port in UseNetMq(...)");
        }

        private static ObvsServiceBuilder UseNetMq(ObvsServiceBuilder service, string whereToSetAddress, string whereToSetPort)
        {
            return service
                .ValidateWith(serviceProvider => Validate(service, GetOptions(serviceProvider, service), whereToSetAddress, whereToSetPort))
                .UseEndpointProvider(serviceProvider => CreateProvider(service, GetOptions(serviceProvider, service)), "NetMQ");
        }

        private static NetMqOptions GetOptions(IServiceProvider serviceProvider, ObvsServiceBuilder service)
        {
            return serviceProvider.GetRequiredService<IOptionsMonitor<NetMqOptions>>().Get(service.Name);
        }

        private static IEnumerable<string> Validate(ObvsServiceBuilder service, NetMqOptions options, string whereToSetAddress, string whereToSetPort)
        {
            if (string.IsNullOrWhiteSpace(options.Address))
            {
                yield return $"Service '{service.Name}' has no NetMQ address. Set {whereToSetAddress}, for example tcp://localhost.";
            }
            if (options.Port == 0)
            {
                yield return $"Service '{service.Name}' has no NetMQ port. Set {whereToSetPort}, for example 5555: the service uses it and the next three.";
            }
            else if (options.Port < 1 || options.Port > HighestPort)
            {
                yield return $"Service '{service.Name}' has NetMQ port {options.Port}, but the service uses it and the next three, " +
                             $"so it must be 1 to {HighestPort}. Change {whereToSetPort}.";
            }
        }

        // The provider is generic in the service's message type, which the builder only knows at run time
        private static IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse> CreateProvider(ObvsServiceBuilder service, NetMqOptions options)
        {
            var providerType = typeof(NetMqServiceEndpointProvider<,,,,,>).MakeGenericType(
                service.ServiceMessageType, typeof(IMessage), typeof(ICommand), typeof(IEvent), typeof(IRequest), typeof(IResponse));

            return (IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>)Activator.CreateInstance(providerType,
                service.Name, options.Address, options.Port, service.Serializer, service.DeserializerFactory, null, null, options.SendHeaders);
        }
    }
}
