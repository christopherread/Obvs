using Azure.Core;

namespace Obvs.AzureSB
{
    /// <summary>
    /// A service's Azure Service Bus settings. Each service has its own, as named options keyed by the service's
    /// name: <c>IOptionsMonitor&lt;AzureServiceBusOptions&gt;.Get("orders")</c>. Set either
    /// <see cref="FullyQualifiedNamespace"/> or <see cref="ConnectionString"/>.
    /// </summary>
    public sealed class AzureServiceBusOptions
    {
        /// <summary>The namespace to connect to, for example <c>contoso.servicebus.windows.net</c>, signing in with <see cref="Credential"/>.</summary>
        public string FullyQualifiedNamespace { get; set; }

        /// <summary>
        /// How to sign in to <see cref="FullyQualifiedNamespace"/>. <c>DefaultAzureCredential</c> (managed identity,
        /// Azure CLI and so on) when not set. Set it in code; it isn't read from configuration.
        /// </summary>
        public TokenCredential Credential { get; set; }

        /// <summary>A connection string with a shared access key, for example for the Service Bus emulator.</summary>
        public string ConnectionString { get; set; }
    }
}
