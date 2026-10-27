# Obvs.NATS

[NATS](https://github.com/nats-io) transport support for [Obvs](https://github.com/christopherread/Obvs).

## NuGet Package

[![NuGet](https://img.shields.io/nuget/v/Obvs.NATS.svg)](https://www.nuget.org/packages/Obvs.NATS/)

See the [NuGet Package Obvs.NATS](https://www.nuget.org/packages/Obvs.NATS/).

## JetStream (v7)

`UseNats` carries messages over core NATS: delivered at most once, to every bus, and kept nowhere. `UseNatsJetStream` carries them over [JetStream](https://docs.nats.io/nats-concepts/jetstream) instead, with the same delivery model as `Obvs.AzureSB`:

```csharp
services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseNatsJetStream(builder.Configuration.GetSection("Obvs:Orders:NatsJetStream")) // Url, MaxDeliveryAttempts, ...
        .SerializedWith(serializer, deserializerFactory)));
```

| Obvs | JetStream |
|---|---|
| Commands, requests | Streams `orders_Commands` and `orders_Requests` (work-queue retention), each read through one consumer, named after the service, that the service's buses share, so each message is handled once |
| Events | Stream `orders_Events` (interest retention), with a consumer per bus named after its instance ID. It's deleted when the bus stops, and deletes itself after 5 minutes idle if the bus crashes |
| Responses | Stream `orders_Responses`; each request names its bus's subject, `orders.Responses.{instance ID}`, read by that bus's own consumer |
| Headers and trace context | NATS headers |
| Dead-lettering | After `MaxDeliveryAttempts` (default 10) failed deliveries, the message is republished to `orders.DeadLetters.{Commands, Events, ...}.{type}` in stream `orders_DeadLetters`, with headers `obvs-dead-letter-reason`, `obvs-dead-letter-description`, `obvs-exception-type` and `obvs-delivery-count` |

- Streams and consumers are created at startup if they're missing. Existing streams are left as they are.
- A message is acknowledged after its handler succeeds, and negatively acknowledged, so it's delivered again, when the handler fails or the bus stops before handling it. Handlers must tolerate duplicates.
- While a handler runs, its message is kept in progress, every third of `AckWait` (JetStream's default of 30 seconds when not set), for up to `MaxInProgressDuration` (5 minutes).
- `MaxConcurrentMessages` (default 1) is how many messages a bus takes from each consumer at once. Raise it together with a handler's `MaxConcurrency`.
- The server must have JetStream enabled (`nats-server -js`). JetStream messages aren't wrapped like core NATS messages, so the two modes don't interoperate.
