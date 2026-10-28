using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Obvs.Types;

namespace Obvs.Tests.Hosting.Handlers
{
    public interface IOrderMessage : IMessage { }

    public interface IShippingMessage : IMessage { }

    public record PlaceOrder(string OrderId) : IOrderMessage, ICommand;

    public record OrderPlaced(string OrderId) : IOrderMessage, IEvent;

    public record GetQuote(string Sku) : IOrderMessage, IRequest<Quote>
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public record Quote(string Sku, decimal Price) : IOrderMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public record ShipOrder(string OrderId) : IShippingMessage, ICommand;

    public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception Exception);

    // Captures everything logged, so tests can check what handlers report
    public sealed class ListLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new ConcurrentQueue<LogEntry>();

        public ILogger CreateLogger(string categoryName) => new ListLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class ListLogger : ILogger
        {
            private readonly ListLoggerProvider _provider;
            private readonly string _category;

            public ListLogger(ListLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _provider.Entries.Enqueue(new LogEntry(logLevel, _category, formatter(state, exception), exception));
            }
        }
    }
}
