using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Types;

namespace Obvs.Testing
{
    /// <summary>
    /// Runs a host with an in-memory broker and waits for messages without fixed delays:
    /// <c>await harness.Published&lt;OrderPlaced&gt;(e => e.OrderId == "o-1")</c> returns as soon as a
    /// matching event passes through the broker, including one that already has.
    /// </summary>
    public sealed class ObvsTestHarness : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly ObvsTestHarnessOptions _options;
        private readonly ReplaySubject<IMessage> _seen = new ReplaySubject<IMessage>();
        private readonly List<IMessage> _messages = new List<IMessage>();
        private readonly IDisposable _recording;

        private ObvsTestHarness(IHost host, InMemoryBroker broker, ObvsTestHarnessOptions options)
        {
            _host = host;
            _options = options;
            Broker = broker;
            _recording = broker.Messages.Subscribe(Record);
        }

        /// <summary>
        /// Builds and starts a host. Configure the bus with <c>services.AddObvs(...)</c> and
        /// <c>UseInMemory()</c> for each service; they all share the harness's broker.
        /// </summary>
        public static async Task<ObvsTestHarness> StartAsync(
            Action<IServiceCollection> configureServices,
            Action<ObvsTestHarnessOptions> configureOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (configureServices == null)
            {
                throw new ArgumentNullException(nameof(configureServices));
            }

            var options = new ObvsTestHarnessOptions();
            configureOptions?.Invoke(options);

            var broker = new InMemoryBroker(options.Scheduler);
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            // Registered before the caller's services, so UseInMemory() picks up this broker
            builder.Services.AddSingleton(broker);
            configureServices(builder.Services);

            var harness = new ObvsTestHarness(builder.Build(), broker, options);
            try
            {
                await harness._host.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await harness.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            return harness;
        }

        /// <summary>The host's services.</summary>
        public IServiceProvider Services => _host.Services;

        /// <summary>The bus, if the services configured one with <c>AddObvs</c>.</summary>
        public IServiceBus Bus => Services.GetService<IServiceBus>();

        /// <summary>The bus, as a client.</summary>
        public IServiceBusClient Client => Services.GetService<IServiceBusClient>();

        /// <summary>The broker carrying every service's messages.</summary>
        public InMemoryBroker Broker { get; }

        /// <summary>Every message that has passed through the broker since the harness started, in order.</summary>
        public IReadOnlyList<IMessage> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToArray();
                }
            }
        }

        /// <summary>Waits for a command to be sent, or returns one already sent. Throws <see cref="ObvsTestTimeoutException"/> if none arrives in time.</summary>
        public Task<TCommand> Sent<TCommand>(Func<TCommand, bool> predicate = null, TimeSpan? timeout = null)
            where TCommand : class, ICommand
        {
            return WaitFor(predicate, timeout, "sent");
        }

        /// <summary>Waits for an event to be published, or returns one already published. Throws <see cref="ObvsTestTimeoutException"/> if none arrives in time.</summary>
        public Task<TEvent> Published<TEvent>(Func<TEvent, bool> predicate = null, TimeSpan? timeout = null)
            where TEvent : class, IEvent
        {
            return WaitFor(predicate, timeout, "published");
        }

        /// <summary>Waits for a request to be sent, or returns one already sent. Throws <see cref="ObvsTestTimeoutException"/> if none arrives in time.</summary>
        public Task<TRequest> Requested<TRequest>(Func<TRequest, bool> predicate = null, TimeSpan? timeout = null)
            where TRequest : class, IRequest
        {
            return WaitFor(predicate, timeout, "requested");
        }

        /// <summary>Waits for a response to be sent, or returns one already sent. Throws <see cref="ObvsTestTimeoutException"/> if none arrives in time.</summary>
        public Task<TResponse> Replied<TResponse>(Func<TResponse, bool> predicate = null, TimeSpan? timeout = null)
            where TResponse : class, IResponse
        {
            return WaitFor(predicate, timeout, "replied");
        }

        /// <summary>Stops the host and disposes its services, including the bus.</summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await _host.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                _recording.Dispose();
                if (_host is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _host.Dispose();
                }
            }
        }

        private void Record(InMemoryMessage message)
        {
            lock (_messages)
            {
                _messages.Add(message.Message);
            }
            _seen.OnNext(message.Message);
        }

        private async Task<TMessage> WaitFor<TMessage>(Func<TMessage, bool> predicate, TimeSpan? timeout, string verb)
            where TMessage : class
        {
            var timeLimit = timeout ?? _options.DefaultTimeout;
            using (var cancellation = new CancellationTokenSource())
            {
                // Both started before the first await, so the timeout runs from when the caller asked
                var match = _seen.OfType<TMessage>().Where(predicate ?? (_ => true)).FirstAsync().ToTask(cancellation.Token);
                var expiry = Delay(timeLimit, cancellation.Token);

                var first = await Task.WhenAny(match, expiry).ConfigureAwait(false);
                cancellation.Cancel();
                if (first == match)
                {
                    return await match.ConfigureAwait(false);
                }
            }

            throw new ObvsTestTimeoutException(DescribeTimeout(typeof(TMessage), verb, timeLimit));
        }

        private Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
#if NET8_0_OR_GREATER
            return Task.Delay(delay, _options.TimeProvider, cancellationToken);
#else
            return _options.TimeProvider.Delay(delay, cancellationToken);
#endif
        }

        private string DescribeTimeout(Type expectedType, string verb, TimeSpan timeLimit)
        {
            var messages = Messages;
            var description = $"No matching {expectedType.Name} was {verb} within {timeLimit}.";
            if (messages.Count == 0)
            {
                return description + " No messages passed through the broker.";
            }

            return description + $" Messages that passed through the broker ({messages.Count}):" + Environment.NewLine +
                   string.Join(Environment.NewLine, messages.Select(message => "- " + Describe(message)));
        }

        // Records print their values; anything else falls back to its type name
        private static string Describe(IMessage message)
        {
            var text = message.ToString();
            return text == message.GetType().ToString() ? message.GetType().Name : text;
        }
    }
}
