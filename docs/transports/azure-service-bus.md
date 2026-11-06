# Azure Service Bus

`Obvs.AzureSB` carries a service's messages over Azure Service Bus. Commands and requests go to queues the service's buses share, so each one is handled once. Events go to a topic, with a subscription for each bus. The queues, topic and subscription are created at startup if they don't exist.

<!-- snippet: asb-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseAzureServiceBus(builder.Configuration.GetSection("Obvs:Orders:AzureServiceBus"))));
```

```json
{
  "Obvs": {
    "Orders": {
      "AzureServiceBus": { "FullyQualifiedNamespace": "contoso.servicebus.windows.net" }
    }
  }
}
```

Or in code:

<!-- snippet: asb-namespace -->
```csharp
service.UseAzureServiceBus(options => options.FullyQualifiedNamespace = "contoso.servicebus.windows.net");
```

Events need a topic, which the Basic tier doesn't have: use a Standard or Premium namespace. On Basic, the bus fails to start and says so.

## How messages map to Service Bus

For a service named `orders`, on a bus with the instance ID `worker-1`:

| Obvs | Azure Service Bus |
|---|---|
| Commands | Queue `orders.Commands`, shared by the service's buses |
| Requests | Queue `orders.Requests`, shared by the service's buses |
| Events | Topic `orders.Events`. Each bus has its own subscription, `worker-1`, so every bus gets every event |
| Responses | Each bus has its own queue, `orders.Responses.worker-1`. A request carries it in `ReplyTo`, and the response is sent there |
| Headers and trace context | Application properties |
| Dead letters | Each queue's and subscription's own dead-letter subqueue, with the reason `HandlerFailed` and the handler's exception as the description |

The instance ID comes from `WithInstanceId(...)`, or is generated for each run when not set. Service Bus allows 50 characters in a subscription name, so the bus fails to start with a longer one. A bus deletes its own subscription and queue when it stops. A bus that crashes leaves them behind, but they're created to delete themselves after 5 minutes idle, the shortest Service Bus allows.

Settings such as `MaxDeliveryAttempts` and `LockDuration` apply only to entities Obvs creates. Entities that already exist keep theirs.

Delivery is at least once: a message whose handler fails, or whose lock expires, is delivered again, so handlers must tolerate duplicates.

## Settings

All of these are in `AzureServiceBusOptions`. Everything except `Credential` can be set from configuration or in code.

| Setting | Default | What it's for |
|---|---|---|
| `FullyQualifiedNamespace` | One of these two is required | The namespace, for example `contoso.servicebus.windows.net`, signed in to with `Credential` |
| `ConnectionString` | | A connection string with a shared access key, for example for the emulator. Set this or `FullyQualifiedNamespace`, not both |
| `Credential` | `DefaultAzureCredential` | A `TokenCredential` to sign in to `FullyQualifiedNamespace` with. Code only |
| `MaxDeliveryAttempts` | 10 | How many times a message is delivered to a failing handler before it's dead-lettered |
| `MaxConcurrentMessages` | The handlers' | How many messages each queue or subscription hands this bus at once. When not set, the sum of the `MaxConcurrency` of the service's handlers of its messages, or 1 if none takes them |
| `LockDuration` | 1 minute | How long a delivered message stays locked to this bus, from 5 seconds to 5 minutes. While a handler runs, the bus renews the lock |
| `MaxHandlerDuration` | 5 minutes | The longest a handler runs with a message, for which the bus keeps renewing its lock. `TimeSpan.Zero` never renews it |

Messages arrive in order only one at a time. For example, to handle up to 8 commands at once, set the handler's `MaxConcurrency`, which the commands queue follows:

<!-- snippet: asb-concurrency -->
```csharp
service
    .UseAzureServiceBus(builder.Configuration.GetSection("Obvs:Orders:AzureServiceBus"))
    .HandleCommand<PlaceOrder>(PlaceOrderAsync, handler => handler.MaxConcurrency = 8);
```

Messages processed with `SubscribeAsync` or Rx aren't visible to the bus at startup: set `MaxConcurrentMessages` for those.

### Credentials

With `FullyQualifiedNamespace`, the bus signs in with `DefaultAzureCredential`: environment variables, workload identity and managed identity and, on a developer's machine, the Azure CLI and the other tools you're signed in to. To sign in some other way, set `Credential` in code. With the other settings in configuration, pass `UseAzureServiceBus` the configuration section, then set `Credential`:

<!-- snippet: asb-credential -->
```csharp
service.UseAzureServiceBus(builder.Configuration.GetSection("Obvs:Orders:AzureServiceBus"),
    options => options.Credential = new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId)));
```

A connection string signs in with a shared access key instead. Keep it out of source control, in user secrets or Key Vault for example.

### Permissions

To create the entities it needs, the bus needs the Manage right: with Microsoft Entra ID, the Azure Service Bus Data Owner role on the namespace; with a shared access key, a policy with Manage. Otherwise it needs Send and Listen (Data Sender and Data Receiver), and the entities must exist before it starts. It checks each one, and if any is missing it fails to start, naming them.

To create them beforehand, with Bicep or Terraform for example, give each bus a fixed instance ID with `WithInstanceId(...)`, as its subscription and reply queue are named after it.

## Not yet checked against Azure

The broker tests run against Microsoft's Service Bus emulator, which takes only a connection string and doesn't emulate every Azure behaviour. These haven't been tried against Azure itself:

- signing in with `DefaultAzureCredential` and the Entra ID roles above;
- telling a Basic-tier namespace from the error it gives when asked for a topic;
- the bus's subscription and reply queue deleting themselves after 5 minutes idle.

## Trying it locally

Microsoft's [Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator) runs in Docker, with Azure SQL Edge as its store. `Obvs.AzureSB.Tests/Emulator/docker-compose.yml` runs it on port 5673, as RabbitMQ has 5672, with management on 5300. Starting it accepts the emulator's and SQL Edge's licence terms:

```bash
docker compose -f Obvs.AzureSB.Tests/Emulator/docker-compose.yml up -d
```

Point the service at it with the emulator's connection string. The bus creates its entities there too, through the emulator's management port:

<!-- snippet: asb-emulator -->
```csharp
service.UseAzureServiceBus(options => options.ConnectionString =
    "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
```

The emulator holds at most 50 queues. The transport's broker tests carry the `Explicit` trait and run against it. Set `AZURE_SERVICE_BUS_CONNECTION_STRING` to use another namespace.

```bash
dotnet test --project Obvs.AzureSB.Tests -- --filter-trait "Category=Explicit"
```
