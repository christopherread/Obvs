# Obvs.Kafka

The Kafka transport for [Obvs](https://github.com/christopherread/Obvs). It works with Confluent Cloud, Amazon MSK and Azure Event Hubs.

```bash
dotnet add package Obvs.Kafka
```

<!-- snippet: kafka-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"))));
```

```json
{ "Obvs": { "Orders": { "Kafka": { "BootstrapServers": "localhost:9092" } } } }
```

For its settings and security, see [Kafka](https://github.com/christopherread/Obvs/blob/master/docs/transports/kafka.md), with guides for [Confluent Cloud](https://github.com/christopherread/Obvs/blob/master/docs/transports/kafka-confluent-cloud.md), [Amazon MSK](https://github.com/christopherread/Obvs/blob/master/docs/transports/kafka-amazon-msk.md) and [Azure Event Hubs](https://github.com/christopherread/Obvs/blob/master/docs/transports/kafka-azure-event-hubs.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
