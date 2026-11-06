# Obvs.NetMQ

The NetMQ (ZeroMQ) transport for [Obvs](https://github.com/christopherread/Obvs). There's no broker: a service's server binds TCP ports, and its clients connect to them.

```bash
dotnet add package Obvs.NetMQ
```

<!-- snippet: netmq-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseNetMq(builder.Configuration.GetSection("Obvs:Orders:NetMq"))));
```

```json
{ "Obvs": { "Orders": { "NetMq": { "Address": "tcp://orders-host", "Port": 5555 } } } }
```

For its ports and settings, see [NetMQ](https://github.com/christopherread/Obvs/blob/master/docs/transports/netmq.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
