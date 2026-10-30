# Common tasks

Short recipes for the things most apps do. Each one is compiled, and run as a test, in `Obvs.Samples`.

## Define a service's messages

A service is a set of messages that share an interface. Each message is a command, an event, a request or a response.

<!-- snippet: messages -->
```csharp
// Every message of a service implements its interface, and is one of ICommand, IEvent, IRequest or IResponse.
// A request names the response that answers it.
public interface IOrderMessage : IMessage { }

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
```

## Add a service

<!-- snippet: add-a-service -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))
        .AddMessages(OrderMessages.Types)));
```

Its settings come from configuration, such as `appsettings.json`:

```json
{ "Obvs": { "Orders": { "RabbitMq": { "Uri": "amqp://localhost:5672" } } } }
```

`AddMessages(OrderMessages.Types)` registers the service's messages from a list Obvs's source generator writes, so nothing is scanned at startup:

<!-- snippet: generated-registry -->
```csharp
// Obvs's source generator gives this a Types property: every message in this assembly that implements IOrderMessage
[ObvsMessages(typeof(IOrderMessage))]
public static partial class OrderMessages { }
```

## Handle a command, and publish an event

<!-- snippet: handle-a-command -->
```csharp
service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
{
    await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
});
```

Or with a class, whose dependencies come from the container:

<!-- snippet: class-handler -->
```csharp
public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrder>
{
    private readonly IOrderStore _orders;

    public PlaceOrderHandler(IOrderStore orders) => _orders = orders;

    public async Task HandleAsync(PlaceOrder command, MessageContext context, CancellationToken ct)
    {
        await _orders.SaveAsync(command.OrderId, ct);
        await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
    }
}
```

<!-- snippet: register-class-handler -->
```csharp
services.AddSingleton<IOrderStore>(store);
services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
    .UseInMemory()
    .HandleCommand<PlaceOrder, PlaceOrderHandler>()));
```

A handler handles one message at a time, in order. For more at once, set `handler => handler.MaxConcurrency = 8`, and the transport's own concurrency, such as Azure Service Bus's `MaxConcurrentCalls`.

## Send a command

From anywhere with the bus, `IServiceBusClient` from the container:

<!-- snippet: send-a-command -->
```csharp
await client.SendAsync(new PlaceOrder("o-1"), ct);
```

## Handle events

<!-- snippet: handle-events -->
```csharp
service.HandleEvent<OrderPlaced>((placed, context, ct) =>
{
    Console.WriteLine($"Order {placed.OrderId} was placed");
    received.TrySetResult(placed.OrderId);
    return Task.CompletedTask;
});
```

And publish one:

<!-- snippet: publish-an-event -->
```csharp
await bus.PublishAsync(new OrderPlaced("o-1"), ct);
```

## Ask and answer: requests and responses

<!-- snippet: handle-a-request -->
```csharp
service.HandleRequest<GetQuote, Quote>(async (request, context, ct) =>
{
    var price = await Task.FromResult(9.99m); // look the price up
    return new Quote(request.Sku, price);
});
```

<!-- snippet: make-a-request -->
```csharp
var quote = await client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);
```

`RequestAsync` throws a `TimeoutException` if no response arrives in time. If the request can't be sent, it throws the transport's error straight away.

## Send headers

Headers travel with a message without being part of it: a tenant, a user, a correlation ID. Pass them when sending:

<!-- snippet: send-headers -->
```csharp
var headers = new Dictionary<string, string> { ["x-tenant"] = "acme" };
await client.SendAsync(new PlaceOrder("o-1"), headers, ct);
```

`PublishAsync` and `RequestAsync` take headers the same way. A handler reads them from its context, and can pass them on:

<!-- snippet: read-headers -->
```csharp
service.HandleCommand<PlaceOrder>(async (command, context, ct) =>
{
    Console.WriteLine($"Order {command.OrderId} for {context.Headers["x-tenant"]}");
    // Pass them on to what the handler sends
    await context.PublishAsync(new OrderPlaced(command.OrderId), context.Headers, ct);
});
```

Obvs sets the trace-context headers, `traceparent` and `tracestate`, on every message itself, replacing any the app passes, so a message passed on continues the current trace. Rx subscribers read the headers a message arrived with from `ObvsMessageHeaders.Get(message)`.

## Read events at your own pace

<!-- snippet: read-events -->
```csharp
await foreach (var placed in client.ReadEventsAsync<OrderPlaced>(ct))
{
    Console.WriteLine($"Order {placed.OrderId} was placed");
    break; // or carry on until ct is cancelled
}
```

## Handle errors

When a handler throws, Obvs logs the exception with the service and message, traces it, and settles the message as the transport allows:
- **Azure Service Bus, Amazon SQS and NATS JetStream** deliver it again, then dead-letter it after `MaxDeliveryAttempts`.
- **The other transports** have already removed it from the broker by then.

<!-- snippet: handle-errors -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseAzureServiceBus(options =>
        {
            options.FullyQualifiedNamespace = "contoso.servicebus.windows.net";
            options.MaxDeliveryAttempts = 5; // then it's dead-lettered
        })
        .HandleCommand<PlaceOrder>((command, context, ct) =>
            // Throwing abandons the command, so it's delivered again
            throw new InvalidOperationException("Out of stock"))));
```

Configuration mistakes stop the app at startup, all at once, each with its fix (see [configuration errors](configuration-errors.md)). Errors a transport reports while running are logged under `Obvs.Bus`.

## Test it

<!-- snippet: test-a-handler -->
```csharp
[Fact]
public async Task PlacingAnOrderPublishesOrderPlaced()
{
    await using var harness = await ObvsTestHarness.StartAsync(services => services
        .AddSingleton<IOrderStore, InMemoryOrderStore>()
        .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
            .UseInMemory()
            .HandleCommand<PlaceOrder, PlaceOrderHandler>())));

    await harness.Client.SendAsync(new PlaceOrder("o-1"));

    var placed = await harness.Published<OrderPlaced>(e => e.OrderId == "o-1");
    Assert.Equal("o-1", placed.OrderId);
}
```

More in [testing](testing.md).

## Trace it

<!-- snippet: trace-it -->
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(ObvsTelemetry.ActivitySourceName).AddConsoleExporter())
    .WithMetrics(metrics => metrics.AddMeter(ObvsTelemetry.MeterName).AddConsoleExporter());
```

Each message's trace follows it from the app that sent it to the one that handles it. Logs come through the app's `ILoggerFactory`.

## Without a host

In a script or a console tool:

<!-- snippet: without-a-host -->
```csharp
await using var bus = await ObvsBus.StartAsync(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseInMemory()
        .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced(command.OrderId), ct))), ct);

await bus.SendAsync(new PlaceOrder("o-1"), ct);
```
