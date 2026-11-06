# Obvs.AzureSB

The Azure Service Bus transport for [Obvs](https://github.com/christopherread/Obvs). Commands and requests go to queues a service's buses share, so each is handled once, and events to a topic. Messages are settled after their handler, and dead-lettered after `MaxDeliveryAttempts`.

```bash
dotnet add package Obvs.AzureSB
```

<!-- snippet: asb-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseAzureServiceBus(builder.Configuration.GetSection("Obvs:Orders:AzureServiceBus"))));
```

```json
{ "Obvs": { "Orders": { "AzureServiceBus": { "FullyQualifiedNamespace": "contoso.servicebus.windows.net" } } } }
```

It signs in with `DefaultAzureCredential` unless you set a `Credential`, or a `ConnectionString`. For how messages map to Service Bus, settings, permissions and the emulator, see [Azure Service Bus](https://github.com/christopherread/Obvs/blob/master/docs/transports/azure-service-bus.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme) and [common tasks](https://github.com/christopherread/Obvs/blob/master/docs/common-tasks.md).
