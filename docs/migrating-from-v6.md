# Migrating from v6 to v7

v7 configures the bus through dependency injection, handles messages with handlers, and awaits requests. The Rx streams are still there. Most changes are additive, and v6 and v7 apps work together on the same broker, so you can move one app at a time.

## v6 and v7 apps together

A v7 service talks to v6 apps on the same broker, with the same exchange, topic and queue names and the same JSON, with these exceptions:

- **JSON:** v7 serializes with System.Text.Json by default, which reads and writes the same JSON as `Obvs.Serialization.Json` (Newtonsoft) for ordinary messages: public properties and fields, matched in any case. If your v6 apps customize Newtonsoft (`JsonSerializerSettings`, converters, enums as strings), keep Newtonsoft in v7 with `SerializedWith(new JsonMessageSerializer(settings), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>), settings))`.
- **Compressed JSON:** `Compressed()` with `Obvs.Serialization.Json` writes the same bytes as v6's `GzippedJson` serializers, and reads theirs.
- **Azure Service Bus and Amazon SQS** are new transports in v7: there are no v6 apps to talk to.
- **ActiveMQ commands over queues:** v7's ActiveMQ transport sends commands over topics, v6's default. A v6 app configured with `UsingQueueFor<ICommand>()` and a v7 app don't reach each other's commands.
- **Retired packages:** NetJSON, XML, YAML and MsgPack.Cli serializers stay on v6. A v7 app can't read what they write. Move those services to JSON, MessagePack (MessagePack-CSharp) or ProtoBuf first, from v6.

`TestV6Interop` in `Obvs.RabbitMQ.Tests` runs a v6 app, on the published 6.x packages in its own process, and a v7 app on one broker, in both directions.

## Configuration

**v6**

<!-- snippet: v6-configuration -->
```csharp
IServiceBus bus = ServiceBus.Configure()
    .WithRabbitMQEndpoints<IOrderMessage>()
        .Named("orders")
        .ConnectToBroker("amqp://localhost:5672")
        .SerializedAsJson()
        .AsClientAndServer()
    .UsingConsoleLogging()
    .Create();
```

**v7**

<!-- snippet: v7-configuration -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))));

// IServiceBus and IServiceBusClient are in the container. The bus starts with the host.
```

Options can come from configuration: `service.UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))`. Without a host, in a script or a console tool:

<!-- snippet: v7-without-a-host -->
```csharp
await using var bus = await ObvsBus.StartAsync(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))));
```

| v6 | v7 |
|---|---|
| `ServiceBus.Configure()` (obsolete) | `services.AddObvs(obvs => ...)`, or `ObvsBus.StartAsync(obvs => ...)` |
| `.WithRabbitMQEndpoints<T>().Named("orders")` | `.AddService<T>("orders", service => service.UseRabbitMq(...))` |
| `.WithKafkaEndpoints<T>()`, `.WithNatsEndpoints<T>()`, `.WithActiveMQEndpoints<T>()` | `UseKafka`, `UseNats` or `UseNatsJetStream`, `UseActiveMq` |
| `.ConnectToBroker(uri)`, `.WithCredentials(user, password)` | The transport's options: `options.Uri`, `options.BrokerUri`, `options.UserName`... |
| `.UsingQueueFor<ICommand>()` (ActiveMQ) | Not in v7 yet: v7's ActiveMQ transport sends commands over topics, as v6 does by default. A v6 app that sends commands to a queue can't reach a v7 server, so move both apps together |
| `.AsClient()`, `.AsServer()`, `.AsClientAndServer()` | The same, on the service. Client and server is the default |
| `.SerializedAsJson()` | Nothing: System.Text.Json is the default. Or `SerializedWith(...)` |
| `.SerializedAsJson(gzipped: true)` | `.Compressed()` |
| `.FilterMessageTypeAssemblies(...)` | `.AddMessages(typeof(PlaceOrder), ...)`, or `[ObvsMessages(typeof(IOrderMessage))]` on a partial class to generate the list |
| `.WithEndpoints(new MyEndpoint())` | `.UseEndpointProvider(serviceProvider => new MyEndpointProvider(), "MyTransport")` |
| `.PublishLocally()` | Not carried over. Use the in-memory transport, `UseInMemory()` from `Obvs.Testing`, for in-process services |
| `.UsingConsoleLogging()`, `.UsingLogging(...)`, `Obvs.Logging.NLog` | `Microsoft.Extensions.Logging`: the bus logs through the app's `ILoggerFactory` |
| `.UsingMonitor(...)`, `Obvs.Monitoring.*` | OpenTelemetry: `tracing.AddSource(ObvsTelemetry.ActivitySourceName)` and `metrics.AddMeter(ObvsTelemetry.MeterName)` |
| `.CreateClient()` | Resolve `IServiceBusClient` |

Configuration errors fail startup, all at once, each with its fix. See [configuration errors](configuration-errors.md).

## Handling messages

**v6**

<!-- snippet: v6-handling -->
```csharp
bus.Commands.OfType<PlaceOrder>().Subscribe(async command =>
{
    await bus.PublishAsync(new OrderPlaced(command.OrderId));
});
```

**v7**

<!-- snippet: v7-handling -->
```csharp
service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
{
    await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
});

// Or a class from the container
service.HandleCommand<PlaceOrder, PlaceOrderHandler>();
```

The v6 code runs as `async void`. Commands are handled at the same time, out of order, and an exception crashes the app. The analyzer reports it as OBVS006. A handler, instead:
- awaits each command, one at a time unless `MaxConcurrency` says otherwise;
- runs in a DI scope;
- logs and traces failures;
- settles the message on transports that keep it until it's handled;
- starts and stops with the host.

`HandleEvent` and `HandleRequest` work the same way. A request handler returns its response.

The streams are still there for what Rx does best: `bus.Events.OfType<OrderPlaced>().Buffer(...)`. To read one at your own pace: `await foreach (var placed in client.ReadEventsAsync<OrderPlaced>(ct))`.

## Requests

**v6**

<!-- snippet: v6-request -->
```csharp
var quote = await bus.GetResponses(new GetQuote("sku-1"))
    .OfType<Quote>().Take(1).Timeout(TimeSpan.FromSeconds(5)).ToTask();
```

**v7**

A request names its response, as `GetQuote : IOrderMessage, IRequest<Quote>` does. That doesn't change its JSON or where it's sent, so v6 apps still understand it. `RequestAsync` infers the response type from it, and `HandleRequest` must answer with it.

<!-- snippet: v7-request -->
```csharp
var quote = await client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);
```

## Cancellation

`SendAsync`, `PublishAsync` and `ReplyAsync` take an optional `CancellationToken`, which the transports pass to their broker clients.

## Custom transports and endpoints

These interfaces gained a `CancellationToken` parameter. Callers compile unchanged, but implementations need the new parameter:
- `IMessagePublisher<T>.PublishAsync`;
- `IServiceEndpoint`'s `PublishAsync` and `ReplyAsync`;
- `IServiceEndpointClient.SendAsync`.

Register a custom transport with `UseEndpointProvider(factory, "MyTransport")`; the name shows in the service's contract. A transport that can't deserialize a message can count it with `ObvsTransportTelemetry.RecordUndeserializable`.

## Errors

- Sending a message that no service on the bus takes throws an `InvalidOperationException` that names the message type and the missing `AddService`. In v6 it threw a plain `Exception`.
- Errors on the `Exceptions` stream are logged, under the category `Obvs.Bus`, as well as published.

## Packages

| v6 package | v7 |
|---|---|
| `Obvs` | `Obvs`, with System.Text.Json, the analyzers and the `[ObvsMessages]` generator built in |
| `Obvs.Serialization.Json` | Kept, for Newtonsoft and v6's settings |
| `Obvs.Serialization.MessagePack-CSharp` | Renamed `Obvs.Serialization.MessagePack` |
| `Obvs.Serialization.MessagePack` (MsgPack.Cli) | Retired. Use `Obvs.Serialization.MessagePack` (MessagePack-CSharp), which isn't wire-compatible with it |
| `Obvs.Serialization.ProtoBuf` | Kept |
| `Obvs.Serialization.NetJson`, `.Xml`, `.Yaml` | Retired: use System.Text.Json |
| `Obvs.Logging.NLog` | Retired: use `Microsoft.Extensions.Logging` with NLog's provider |
| `Obvs.Monitoring.AppMetrics`, `.ElasticSearch`, `.PerformanceCounters` | Retired: use OpenTelemetry |
| `Obvs.RabbitMQ`, `.Kafka`, `.NATS`, `.ActiveMQ`, `.NetMQ` | Kept |
| — | New: `Obvs.AzureSB`, `Obvs.AmazonSqs`, `Obvs.Testing`, `Obvs.Mcp` |

## What else is new

- **Analyzers** catch common mistakes as you type ([rules](analyzers.md)).
- **`Obvs.Testing`** runs services against an in-memory broker in tests ([guide](testing.md)).
- **Contracts:** each service's contract is an AsyncAPI document, and a test can catch breaking changes ([contracts](contracts.md)).
- **`Obvs.Mcp`** lets an agent inspect and drive a development bus ([guide](mcp.md)).
- **The examples** in `Examples/` show a client and a service on ActiveMQ, with handlers, a request, and a trace that crosses both apps.
