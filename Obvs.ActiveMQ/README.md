# Obvs.ActiveMQ

The ActiveMQ transport for [Obvs](https://github.com/christopherread/Obvs).

```bash
dotnet add package Obvs.ActiveMQ
```

<!-- snippet: activemq-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseActiveMq(builder.Configuration.GetSection("Obvs:Orders:ActiveMq"))));
```

```json
{ "Obvs": { "Orders": { "ActiveMq": { "BrokerUri": "tcp://localhost:61616" } } } }
```

Its settings are in `ActiveMqOptions`: `BrokerUri`, `UserName` and `Password`.

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
