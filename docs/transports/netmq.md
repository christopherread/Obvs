# NetMQ

`Obvs.NetMQ` carries a service's messages over [NetMQ](https://github.com/zeromq/netmq), a .NET port of ZeroMQ. There's no broker: the service's server binds four TCP ports, and its clients connect to them.

<!-- snippet: netmq-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseNetMq(builder.Configuration.GetSection("Obvs:Orders:NetMq"))));
```

```json
{
  "Obvs": {
    "Orders": {
      "NetMq": { "Address": "tcp://orders-host", "Port": 5555 }
    }
  }
}
```

Or in code:

<!-- snippet: netmq-in-code -->
```csharp
service.UseNetMq(options =>
{
    options.Address = "tcp://orders-host";
    options.Port = 5555;
});
```

## How messages map to NetMQ

For a service named `orders` on port 5555, as in v6:

| Obvs | NetMQ |
|---|---|
| Requests | Port 5555, topic `orders.Requests` |
| Responses | Port 5556, topic `orders.Responses` |
| Commands | Port 5557, topic `orders.Commands` |
| Events | Port 5558, topic `orders.Events` |

The server binds each port, so one app is the service's server. Its clients connect, and can start before it.

## Delivery

- Messages are delivered at most once, and kept nowhere. A message sent while no one is connected is lost. There's no dead-lettering, so a handler that fails is logged and traced only.
- ZeroMQ drops what's published before a subscriber has joined. So the first message on each socket waits, for up to five seconds, until a subscriber to its topic has.
- Use NetMQ for low-latency messaging inside one network, where losing a message is acceptable. For delivery that survives restarts, use a broker.

## Headers and v6 apps

v6 apps read exactly four frames: the topic, the message's type name, its data type and its body. So by default v7 sends the same, without the message's headers, and its trace context doesn't reach the receiver.

Once every app on the service is on v7, set `SendHeaders` to send headers, such as trace context, in a fifth frame:

```json
"NetMq": { "Address": "tcp://orders-host", "Port": 5555, "SendHeaders": true }
```

v7 apps read messages with or without headers, so turn it on app by app.
