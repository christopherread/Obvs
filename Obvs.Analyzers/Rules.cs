using Microsoft.CodeAnalysis;

namespace Obvs.Analyzers
{
    internal static class Rules
    {
        private const string Category = "Obvs";
        private const string HelpLinkBase = "https://github.com/christopherread/Obvs/blob/master/docs/analyzers.md#";

        // The properties a code fix needs
        public const string ServiceInterfaceProperty = "ServiceInterface";

        public static readonly DiagnosticDescriptor MessageOfAnotherService = Create(
            "OBVS001",
            "A message registered or handled on a service doesn't implement its message interface",
            "Service '{0}' registers or handles {1}, which doesn't implement {2}. Make {1} implement {2}, or register it on the service it belongs to.");

        public static readonly DiagnosticDescriptor MoreThanOneKind = Create(
            "OBVS002",
            "A message is more than one kind of message",
            "{0} implements {1}, but a message must be exactly one of ICommand, IEvent, IRequest or IResponse. Split it into one message of each kind.");

        public static readonly DiagnosticDescriptor ResponseOfAnotherService = Create(
            "OBVS003",
            "A request handler's response doesn't belong to the service",
            "Service '{0}' answers {1} with {2}, which doesn't implement {3}, so it's sent where no client listens. Make {2} implement {3}.");

        public static readonly DiagnosticDescriptor NoTransport = Create(
            "OBVS004",
            "A service has no transport",
            "Service '{0}' has no transport, so the bus fails to start. Call one on it, such as UseRabbitMq, UseKafka, UseNats, UseAzureServiceBus or UseAmazonSqs, or UseInMemory in tests.");

        public static readonly DiagnosticDescriptor DiscardedSubscription = Create(
            "OBVS005",
            "The subscription to a bus stream is discarded",
            "The subscription to this bus stream is discarded, so it can't be stopped, and it keeps the bus's connection busy until the app ends. Keep it and dispose it when you're done, or handle the messages with HandleCommand, HandleEvent or HandleRequest.");

        public static readonly DiagnosticDescriptor AsyncVoidSubscriber = Create(
            "OBVS006",
            "An async lambda subscribes to a bus stream",
            "This async lambda runs as async void: messages are handled at the same time and out of order, and an exception crashes the app. Use HandleCommand, HandleEvent or HandleRequest, or run each in turn with Select(m => Observable.FromAsync(...)).Concat().");

        private static DiagnosticDescriptor Create(string id, string title, string message)
        {
            return new DiagnosticDescriptor(id, title, message, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
                helpLinkUri: HelpLinkBase + id.ToLowerInvariant());
        }
    }
}
