# Obvs.RabbitMQ

The RabbitMQ transport for [Obvs](https://github.com/christopherread/Obvs). Each service publishes to a topic exchange, and each bus reads it through its own queue.

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

Its settings are in `RabbitMqOptions`.

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
