using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obvs.Hosting;
using Obvs.Serialization.SystemTextJson;
using Obvs.Types;

namespace Obvs.Mcp
{
    internal sealed record RecordedMessage(DateTimeOffset Time, string Service, string Type, JsonNode Message);

    internal sealed record RecordedError(DateTimeOffset Time, string Category, string Message, string Error, string TraceId);

    // The bus's recent messages, for watch: the commands and requests it receives as a server, and the events as a client
    internal sealed class ObvsMcpMessages : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ObvsMcpOptions _options;
        private readonly LinkedList<RecordedMessage> _messages = new LinkedList<RecordedMessage>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private Dictionary<Type, string> _services = new Dictionary<Type, string>();
        private event Action Recorded;

        public ObvsMcpMessages(IServiceProvider serviceProvider, ObvsMcpOptions options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var contracts = _serviceProvider.GetRequiredService<ObvsContracts>();
            _services = contracts.ServiceNames
                .SelectMany(service => contracts.MessageTypes(service).Select(type => (type, service)))
                .GroupBy(pair => pair.type)
                .ToDictionary(group => group.Key, group => group.First().service);

            var bus = _serviceProvider.GetRequiredService<IServiceBus>();
            _subscriptions.Add(bus.Commands.Subscribe(Record, _ => { }));
            _subscriptions.Add(bus.Requests.Subscribe(Record, _ => { }));
            _subscriptions.Add(bus.Events.Subscribe(Record, _ => { }));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscriptions.ForEach(subscription => subscription.Dispose());
            _subscriptions.Clear();
            return Task.CompletedTask;
        }

        public void Record(IMessage message)
        {
            var type = message.GetType();
            var recorded = new RecordedMessage(DateTimeOffset.UtcNow, _services.TryGetValue(type, out var service) ? service : null, type.Name,
                System.Text.Json.JsonSerializer.SerializeToNode(message, type, SystemTextJsonMessageDefaults.Options));
            lock (_messages)
            {
                _messages.AddLast(recorded);
                while (_messages.Count > _options.RecentMessages)
                {
                    _messages.RemoveFirst();
                }
            }
            Recorded?.Invoke();
        }

        // Matching messages since `since`, waiting up to `wait` for the first
        public async Task<IReadOnlyList<RecordedMessage>> WatchAsync(Func<RecordedMessage, bool> matches, DateTimeOffset since, TimeSpan wait, int max,
            CancellationToken cancellationToken)
        {
            var arrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnRecorded() => arrived.TrySetResult(true);
            Recorded += OnRecorded;
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(wait);
                    while (true)
                    {
                        var found = Matching(matches, since, max);
                        if (found.Count > 0 || timeout.IsCancellationRequested)
                        {
                            return found;
                        }
                        arrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        await Task.WhenAny(arrived.Task, Task.Delay(Timeout.Infinite, timeout.Token)).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                Recorded -= OnRecorded;
            }
        }

        private IReadOnlyList<RecordedMessage> Matching(Func<RecordedMessage, bool> matches, DateTimeOffset since, int max)
        {
            lock (_messages)
            {
                return _messages.Where(message => message.Time >= since && matches(message)).Take(max).ToArray();
            }
        }
    }

    // Errors Obvs logs, for recent_errors: handler failures, and errors transports report on the bus
    internal sealed class ObvsMcpErrors : ILoggerProvider
    {
        private readonly ObvsMcpOptions _options;
        private readonly LinkedList<RecordedError> _errors = new LinkedList<RecordedError>();

        public ObvsMcpErrors(ObvsMcpOptions options)
        {
            _options = options;
        }

        public IReadOnlyList<RecordedError> Recent(int max)
        {
            lock (_errors)
            {
                return _errors.Reverse().Take(max).ToArray();
            }
        }

        public ILogger CreateLogger(string categoryName) => new ErrorLogger(this, categoryName);

        public void Dispose()
        {
        }

        private void Add(RecordedError error)
        {
            lock (_errors)
            {
                _errors.AddLast(error);
                while (_errors.Count > _options.RecentErrors)
                {
                    _errors.RemoveFirst();
                }
            }
        }

        private sealed class ErrorLogger : ILogger
        {
            private readonly ObvsMcpErrors _errors;
            private readonly string _category;

            public ErrorLogger(ObvsMcpErrors errors, string category)
            {
                _errors = errors;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error && _category.StartsWith("Obvs", StringComparison.Ordinal);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }
                _errors.Add(new RecordedError(DateTimeOffset.UtcNow, _category, formatter(state, exception),
                    exception == null ? null : $"{exception.GetType().FullName}: {exception.Message}", Activity.Current?.TraceId.ToString()));
            }
        }
    }
}
