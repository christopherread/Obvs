# Obvs

Messaging between .NET services. Each service has commands, events, requests and responses. You choose its transport: RabbitMQ, Kafka, NATS, ActiveMQ, Azure Service Bus, Amazon SQS or NetMQ, each in its own package. Your code handles its messages, or reads them as Reactive Extensions streams.

This package has the bus, hosting with `AddObvs`, handlers, System.Text.Json serialization, OpenTelemetry, the analyzers and the `[ObvsMessages]` source generator.

```bash
dotnet add package Obvs
dotnet add package Obvs.RabbitMQ
```

Define your service's messages:

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

Host the service: it handles commands and requests, and publishes events.

<!-- snippet: quick-start-service -->
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))
        .HandleCommand<PlaceOrder>(async (command, context, ct) =>
        {
            // ... place the order, then tell whoever's interested
            await context.PublishAsync(new OrderPlaced(command.OrderId), ct);
        })
        .HandleRequest<GetQuote, Quote>((request, context, ct) =>
            Task.FromResult(new Quote(request.Sku, 9.99m)))));

await builder.Build().RunAsync();
```

Then send it commands and requests from another app, a client of the service:

<!-- snippet: quick-start-client -->
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(options => options.Uri = new Uri("amqp://localhost:5672"))
        .AsClient()));

using var app = builder.Build();
await app.StartAsync();

var client = app.Services.GetRequiredService<IServiceBusClient>();

await client.SendAsync(new PlaceOrder("o-1"));

var quote = await client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5));
```

## Next steps

- The [README](https://github.com/christopherread/Obvs#readme), and the [Obvs site](https://christopherread.github.io/Obvs/).
- [Common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md): add a service, handle and send messages, requests, errors, tests, tracing.
- [Configuration errors](https://github.com/christopherread/Obvs/blob/master/docs/configuration-errors.md) and [analyzers](https://github.com/christopherread/Obvs/blob/master/docs/analyzers.md).
- [Migrating from v6](https://github.com/christopherread/Obvs/blob/master/docs/migrating-from-v6.md).
