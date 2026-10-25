using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Obvs.Hosting
{
    /// <summary>
    /// Creates the bus when the host starts, so it connects and subscribes before the app takes traffic,
    /// and configuration errors stop startup instead of surfacing on first use.
    /// The container, not this service, disposes the bus.
    /// </summary>
    internal sealed class ObvsHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _key;

        public ObvsHostedService(IServiceProvider serviceProvider, string key)
        {
            _serviceProvider = serviceProvider;
            _key = key;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_key == null)
            {
                _serviceProvider.GetRequiredService<IServiceBus>();
            }
            else
            {
                _serviceProvider.GetRequiredKeyedService<IServiceBus>(_key);
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
