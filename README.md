# Obvs: an observable microservice bus
## observable services, *obviously*

[![Join the chat at https://gitter.im/inter8ection/Obvs](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/inter8ection/Obvs?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![.NET](https://github.com/christopherread/Obvs/workflows/.NET/badge.svg)](https://github.com/christopherread/Obvs/actions)

[![NuGet](https://img.shields.io/nuget/v/Obvs.svg)](https://www.nuget.org/packages/Obvs/)

Obvs is a .NET library for messaging between services. Each service has commands, events, requests and responses. You choose its transport, such as RabbitMQ, Kafka, NATS, Azure Service Bus or Amazon SQS. Your code handles its messages, or reads them as Reactive Extensions streams.

## Quick start

```
dotnet add package Obvs
dotnet add package Obvs.RabbitMQ
```

Define your service's messages:

<!-- snippet: messages -->
```csharp
// Every message of a service implements its interface, and is one of ICommand, IEvent, IRequest or IResponse
public interface IOrderMessage : IMessage { }

public record PlaceOrder(string OrderId) : IOrderMessage, ICommand;

public record OrderPlaced(string OrderId) : IOrderMessage, IEvent;

public record GetQuote(string Sku) : IOrderMessage, IRequest
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

And use it from anywhere with the bus:

<!-- snippet: quick-start-client -->
```csharp
var client = app.Services.GetRequiredService<IServiceBusClient>();

await client.SendAsync(new PlaceOrder("o-1"), ct);

var quote = await client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);
```

## Next steps

- [Common tasks](docs/common-tasks.md): add a service, handle and send messages, requests, errors, tests, tracing.
- [Testing](docs/testing.md) with an in-memory broker.
- Transports: [Kafka](docs/transports/kafka.md) (and [Confluent Cloud](docs/transports/kafka-confluent-cloud.md), [Amazon MSK](docs/transports/kafka-amazon-msk.md), [Azure Event Hubs](docs/transports/kafka-azure-event-hubs.md)), [Amazon SQS and SNS](docs/transports/amazon-sqs.md).
- [Configuration errors](docs/configuration-errors.md) and [analyzers](docs/analyzers.md).
- [Service contracts](docs/contracts.md) as AsyncAPI, and an [MCP server](docs/mcp.md) for agents.
- [Migrating from v6](docs/migrating-from-v6.md).
- [`llms.txt`](llms.txt), for agents, and [`AGENTS.md`](AGENTS.md), for working on Obvs itself.

## Packages

| Package | |
|---|---|
| `Obvs` | The bus, hosting with `AddObvs`, handlers, System.Text.Json serialization, OpenTelemetry, the analyzers and the `[ObvsMessages]` generator |
| `Obvs.RabbitMQ`, `Obvs.Kafka`, `Obvs.NATS`, `Obvs.ActiveMQ`, `Obvs.AzureSB`, `Obvs.AmazonSqs`, `Obvs.NetMQ` | Transports. `Obvs.NATS` covers core NATS and JetStream |
| `Obvs.Serialization.Json`, `.MessagePack`, `.ProtoBuf` | Other serializers: Newtonsoft JSON, MessagePack-CSharp and protobuf-net. Any serializer can be gzipped with `Compressed()` |
| `Obvs.Testing` | An in-memory broker and a test harness |
| `Obvs.Mcp` | An MCP server for development buses |

The XML, NetJSON, YAML and MsgPack.Cli serializers, `Obvs.Monitoring.*` and `Obvs.Logging.NLog` stay on v6.

## Versions

* v7 - hosting with dependency injection, handlers, `RequestAsync`, System.Text.Json, OpenTelemetry, Azure Service Bus and Amazon SQS, analyzers, a test kit, contracts and an MCP server. Supports `netstandard2.0`, `net472` and `net10.0`
* v6 - `System.Reactive 5.0`, supports `netstandard2.0`, `net472` and `net5.0`. Mono-repo
* v5 - `System.Reactive 4.1`, supports `netstandard2.0` and `net472`
* v4 - `System.Reactive 3.1.1`, supports `netstandard1.6` and `net452`

## Examples

`Examples/` has a client and a service on ActiveMQ, traced across both apps with OpenTelemetry's console exporter:

	cd Examples
	docker compose up        # ActiveMQ and the service
	dotnet run --project Client
