using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Obvs.Hosting
{
    /// <summary>
    /// Creates the bus when the host starts, so it connects and subscribes before the app takes traffic,
    /// and configuration errors stop startup instead of surfacing on first use. Starts the services'
    /// handlers, and on stop waits for running ones. The container, not this service, disposes the bus.
    /// </summary>
    internal sealed class ObvsHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _key;
        private readonly ObvsBuilder _builder;
        private readonly List<HandlerRunner> _handlers = new List<HandlerRunner>();

        public ObvsHostedService(IServiceProvider serviceProvider, string key, ObvsBuilder builder)
        {
            _serviceProvider = serviceProvider;
            _key = key;
            _builder = builder;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var bus = _key == null
                ? _serviceProvider.GetRequiredService<IServiceBus>()
                : _serviceProvider.GetRequiredKeyedService<IServiceBus>(_key);

            var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("Obvs.Hosting.Handlers") ?? NullLogger.Instance;
            _handlers.AddRange(_builder.ServiceBuilders.SelectMany(service => service.Handlers
                .Select(handler => handler.Start(bus, service.Name, _serviceProvider, logger))));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(_handlers.Select(handler => handler.StopAsync(cancellationToken)));
        }
    }
}
