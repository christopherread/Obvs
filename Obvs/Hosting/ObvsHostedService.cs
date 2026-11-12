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
        private IDisposable _errorLogging;

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

            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

            // Transports report errors on the bus's Exceptions stream, which apps often don't subscribe to
            var busLogger = loggerFactory?.CreateLogger("Obvs.Bus") ?? NullLogger.Instance;
            _errorLogging = bus.Exceptions.Subscribe(exception => busLogger.LogError(exception, "The Obvs bus reported an error: {Message}", exception.Message));

            var logger = loggerFactory?.CreateLogger("Obvs.Hosting.Handlers") ?? NullLogger.Instance;
            _handlers.AddRange(_builder.ServiceBuilders.SelectMany(service => service.Handlers
                .Select(handler => handler.Start(bus, service.Name, _serviceProvider, logger))));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.WhenAll(_handlers.Select(handler => handler.StopAsync(cancellationToken))).ConfigureAwait(false);
            }
            finally
            {
                _errorLogging?.Dispose();
            }
        }
    }
}
