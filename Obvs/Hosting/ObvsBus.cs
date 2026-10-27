using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Types;

namespace Obvs.Hosting
{
    /// <summary>
    /// A bus for scripts and console tools that have no host:
    /// <c>await using var bus = await ObvsBus.StartAsync(obvs => obvs.AddService&lt;IOrderMessage&gt;(...));</c>
    /// It's configured as with <c>AddObvs</c>, in a container of its own, and starts as a host would start it:
    /// configuration errors are thrown from <see cref="StartAsync(Action{ObvsBuilder}, CancellationToken)"/>, and
    /// handlers run until it's disposed. Disposing it waits for running handlers, then disposes the bus.
    /// </summary>
    public sealed class ObvsBus : IServiceBus, IAsyncDisposable, IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly IReadOnlyList<IHostedService> _started;
        private readonly IServiceBus _bus;
        private int _disposed;

        private ObvsBus(ServiceProvider services, IReadOnlyList<IHostedService> started)
        {
            _services = services;
            _started = started;
            _bus = services.GetRequiredService<IServiceBus>();
        }

        /// <summary>Configures and starts a bus.</summary>
        public static Task<ObvsBus> StartAsync(Action<ObvsBuilder> configure, CancellationToken cancellationToken = default)
        {
            return StartAsync(services => { }, configure, cancellationToken);
        }

        /// <summary>
        /// Configures and starts a bus, with <paramref name="configureServices"/> adding what its handlers and transports
        /// need to its container, such as logging or a handler's dependencies.
        /// </summary>
        public static async Task<ObvsBus> StartAsync(Action<IServiceCollection> configureServices, Action<ObvsBuilder> configure,
            CancellationToken cancellationToken = default)
        {
            if (configureServices == null)
            {
                throw new ArgumentNullException(nameof(configureServices));
            }
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var services = new ServiceCollection();
            configureServices(services);
            services.AddObvs(configure);
            var provider = services.BuildServiceProvider();

            var started = new List<IHostedService>();
            try
            {
                foreach (var hostedService in provider.GetServices<IHostedService>())
                {
                    await hostedService.StartAsync(cancellationToken).ConfigureAwait(false);
                    started.Add(hostedService);
                }
                return new ObvsBus(provider, started);
            }
            catch
            {
                await StopAsync(started).ConfigureAwait(false);
                await provider.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>The bus's container, with the services given to <c>StartAsync</c>.</summary>
        public IServiceProvider Services => _services;

        /// <inheritdoc />
        public IObservable<IEvent> Events => _bus.Events;

        /// <inheritdoc />
        public IObservable<IRequest> Requests => _bus.Requests;

        /// <inheritdoc />
        public IObservable<ICommand> Commands => _bus.Commands;

        /// <inheritdoc />
        public IObservable<Exception> Exceptions => _bus.Exceptions;

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken = default) => _bus.SendAsync(command, cancellationToken);

        /// <inheritdoc />
        public Task SendAsync(IEnumerable<ICommand> commands, CancellationToken cancellationToken = default) => _bus.SendAsync(commands, cancellationToken);

        /// <inheritdoc />
        public Task PublishAsync(IEvent ev, CancellationToken cancellationToken = default) => _bus.PublishAsync(ev, cancellationToken);

        /// <inheritdoc />
        public Task ReplyAsync(IRequest request, IResponse response, CancellationToken cancellationToken = default) => _bus.ReplyAsync(request, response, cancellationToken);

        /// <inheritdoc />
        public IObservable<IResponse> GetResponses(IRequest request) => _bus.GetResponses(request);

        /// <inheritdoc />
        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse => _bus.GetResponses<T>(request);

        /// <inheritdoc />
        public IObservable<T> GetResponse<T>(IRequest request) where T : IResponse => _bus.GetResponse<T>(request);

        /// <summary>Waits for running handlers to finish, then disposes the bus and its container.</summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            try
            {
                await StopAsync(_started).ConfigureAwait(false);
            }
            finally
            {
                await _services.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        // In reverse order of starting, as a host stops its services
        private static async Task StopAsync(IEnumerable<IHostedService> started)
        {
            foreach (var hostedService in started.Reverse())
            {
                await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
