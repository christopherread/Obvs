# Configuration errors

When a host starts, Obvs checks the whole bus configuration first. If anything is wrong, it throws one `ObvsConfigurationException`, whose `Errors` lists every problem it found, not just the first. Each message names the service, what's wrong, and the API that fixes it.

This page lists every check. Each one has a test that asserts its message names the fix: the core's checks in `Obvs.Tests`, and each transport's in its own `TestUse<Transport>` tests.

## Services

| Problem | Fix |
|---|---|
| Two services on a bus share a name | Give each service a unique name in `AddService<T>(name, ...)`. |
| A service has no transport | Call a transport on it: `UseRabbitMq`, `UseKafka`, `UseNats`, `UseNatsJetStream`, `UseActiveMq`, `UseAzureServiceBus` or `UseAmazonSqs` from its package; `UseInMemory` from `Obvs.Testing` for tests; or `UseEndpointProvider(...)` for your own. |
| A check added with `ValidateWith(...)` fails | Its own message says what to fix. |

A service with no `SerializedWith(...)` serializes its messages with System.Text.Json. It reads and writes the same JSON as `Obvs.Serialization.Json`, so v6 services can read its messages, and it can read theirs.

## Messages registered with `AddMessages`

| Problem | Fix |
|---|---|
| A registered type doesn't implement the service's message interface | Make it implement the interface, or register it on the service it belongs to. |
| A registered type isn't exactly one of `ICommand`, `IEvent`, `IRequest` or `IResponse` | Implement exactly one of them. |
| A registered type is an interface or abstract class | Register the concrete message classes. |
| A registered type isn't public | Make it public. |

## Handlers

| Problem | Fix |
|---|---|
| A handler's message doesn't implement the service's message interface | Register the handler on the service the message belongs to. |
| A command or request handler is on a service configured `AsClient()` | Call `AsServer()` or `AsClientAndServer()`. Only servers receive commands and requests. |
| An event handler is on a service configured `AsServer()` | Call `AsClient()` or `AsClientAndServer()`. Only clients receive events. |
| A handler's `MaxConcurrency` is less than 1 | Set it to 1 or more. |
| A service registers its messages, but not one it handles | Register it with `AddMessages(typeof(T))`. A service that registers its messages receives only those, so the handler would never run. |
| A command or request has more than one handler | Merge them into one. If several parts of the app should react, have the handler publish an event, and handle that with `HandleEvent`. An event can have any number of handlers. |

## Transports

Each transport checks its own options. The message says which option to set, and where: in code, or under the configuration section passed to `Use<Transport>(IConfiguration)`.

| Transport | Checks |
|---|---|
| RabbitMQ | A broker address is set. |
| ActiveMQ | A broker URI is set. |
| NATS | A server address is set. |
| NATS JetStream | <ul><li>A server address is set.</li><li>`MaxDeliveryAttempts` and `MaxConcurrentMessages` are at least 1.</li><li>`AckWait` is positive, and `MaxInProgressDuration` isn't negative.</li><li>The service name and instance ID are valid stream and consumer names.</li></ul> |
| Kafka | <ul><li>Bootstrap servers are set.</li><li>`SecurityProtocol`, `SaslMechanism`, credentials, the OAuth token provider and TLS files are consistent with each other.</li><li>The settings match what Confluent Cloud, Amazon MSK and Azure Event Hubs need, when the bootstrap servers are theirs.</li></ul> |
| Azure Service Bus | <ul><li>Exactly one of a namespace or a connection string is set.</li><li>`MaxDeliveryAttempts` and `MaxConcurrentCalls` are at least 1.</li><li>`LockDuration` is between 5 seconds and 5 minutes, and `MaxLockRenewalDuration` isn't negative.</li><li>The instance ID fits in a subscription name.</li></ul> At startup, it also reports a Basic-tier namespace, and any missing entities when the credentials lack the Manage right. |
| Amazon SQS | <ul><li>A region is set, and `ServiceUrl` is an http or https URL.</li><li>`MaxDeliveryAttempts`, `MaxConcurrentCalls`, `VisibilityTimeout` and `MaxHandlerDuration` are within SQS's limits.</li><li>`VisibilityTimeout` is longer than `MaxHandlerDuration`.</li><li>The clean-up intervals make sense.</li><li>Queue and topic names fit SQS's character set and length.</li></ul> |

## While running

Sending a message that no service on the bus takes throws an `InvalidOperationException` that names the message type, and says to add its service with `AddService<T>(...)`.
