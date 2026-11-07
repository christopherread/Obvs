# Obvs.RabbitMQ

The RabbitMQ transport for [Obvs](https://github.com/christopherread/Obvs). Commands and requests go to quorum queues each service's servers share, and stay there until they're handled. Events go to every bus.

```bash
dotnet add package Obvs.RabbitMQ
```

<!-- snippet: rabbitmq-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))));
```

```json
{ "Obvs": { "Orders": { "RabbitMq": { "Uri": "amqp://localhost:5672" } } } }
```

Its settings are in `RabbitMqOptions`. How messages map to RabbitMQ, and how they're settled and dead-lettered, is in the [RabbitMQ guide](https://github.com/christopherread/Obvs/blob/master/docs/transports/rabbitmq.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
