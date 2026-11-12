# Obvs.AmazonSqs

The Amazon SQS and SNS transport for [Obvs](https://github.com/christopherread/Obvs). Commands and requests go to SQS queues a service's buses share, so each is handled once, and events to an SNS topic. Messages are settled after their handler, and dead-lettered after `MaxDeliveryAttempts`.

```bash
dotnet add package Obvs.AmazonSqs
```

<!-- snippet: sqs-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"))));
```

```json
{ "Obvs": { "Orders": { "AmazonSqs": { "Region": "eu-west-1" } } } }
```

For how messages map to SQS and SNS, settings, IAM permissions and LocalStack, see [Amazon SQS and SNS](https://github.com/christopherread/Obvs/blob/master/docs/transports/amazon-sqs.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
