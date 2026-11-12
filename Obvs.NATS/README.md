# Obvs.NATS

The NATS transport for [Obvs](https://github.com/christopherread/Obvs). `UseNats` carries messages over core NATS, at most once. `UseNatsJetStream` carries them over JetStream: commands and requests on consumers a service's buses share, so each is handled once, with acknowledgement after the handler and dead-lettering.

```bash
dotnet add package Obvs.NATS
```

<!-- snippet: nats-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseNats(builder.Configuration.GetSection("Obvs:Orders:Nats"))));
```

```json
{ "Obvs": { "Orders": { "Nats": { "Url": "nats://localhost:4222" } } } }
```

For JetStream, its streams, consumers and settings, see [NATS](https://github.com/christopherread/Obvs/blob/master/docs/transports/nats.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
