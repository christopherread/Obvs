# Kafka on Azure Event Hubs

Event Hubs has a Kafka endpoint in the Standard, Premium and Dedicated tiers; Basic has none. It differs from a Kafka cluster in a few ways:

- The bootstrap server is the namespace on **port 9093**: `<namespace>.servicebus.windows.net:9093`.
- It needs **`SASL_SSL`**. Sign in with **`PLAIN`**, using the user name `$ConnectionString` and a connection string as the password, or with **`OAUTHBEARER`** and Microsoft Entra ID tokens.
- A Kafka topic is an event hub. The Standard tier allows 10 event hubs per namespace, and each Obvs service uses four, so on Standard a namespace holds two services.

The startup checks know the first two: a namespace on another port, a protocol other than `SASL_SSL`, a `PLAIN` user name other than `$ConnectionString`, or a `SCRAM` mechanism stops the bus starting, with an error saying what to set.

> Obvs's tests run against a local Kafka broker with SASL, not against Event Hubs itself.

For the full rules, see Microsoft's [Kafka protocol support in Event Hubs](https://learn.microsoft.com/azure/event-hubs/azure-event-hubs-apache-kafka-overview) and [quotas](https://learn.microsoft.com/azure/event-hubs/event-hubs-quotas).

## Connection string

Copy a connection string from the namespace's **Shared access policies**. For the bus to create its event hubs at startup, the policy needs **Manage**. With only **Send** and **Listen**, or if creating them at startup fails, create the four event hubs beforehand.

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "contoso.servicebus.windows.net:9093",
        "SecurityProtocol": "SASL_SSL",
        "SaslMechanism": "PLAIN",
        "SaslUsername": "$ConnectionString",
        "SaslPassword": "Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<key>"
      }
    }
  }
}
```

```csharp
service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"));
```

The user name is the literal text `$ConnectionString`. Keep the connection string itself out of `appsettings.json`: use user secrets locally, and an environment variable such as `Obvs__Orders__Kafka__SaslPassword` or Key Vault when deployed.

## Microsoft Entra ID

Sign in with a managed identity, or any other Azure credential, by getting tokens with [`Azure.Identity`](https://www.nuget.org/packages/Azure.Identity) for the namespace's scope:

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "contoso.servicebus.windows.net:9093",
        "SecurityProtocol": "SASL_SSL",
        "SaslMechanism": "OAUTHBEARER"
      }
    }
  }
}
```

```csharp
using Azure.Core;
using Azure.Identity;

var credential = new DefaultAzureCredential();
var scope = new TokenRequestContext(new[] { "https://contoso.servicebus.windows.net/.default" });

service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
    options.OAuthBearerTokenProvider = async cancellationToken =>
    {
        var token = await credential.GetTokenAsync(scope, cancellationToken);
        return new KafkaOAuthBearerToken(token.Token, token.ExpiresOn);
    });
```

Give the identity **Azure Event Hubs Data Sender** and **Azure Event Hubs Data Receiver** on the namespace. Creating event hubs takes more, such as **Azure Event Hubs Data Owner**; without it, create the four event hubs beforehand.

## Consumer groups

Event Hubs allows **1,000 Kafka consumer groups per namespace**, in each tier with a Kafka endpoint. Kafka consumer groups are created as they're used, and don't need creating in the portal like Event Hubs' own consumer groups.

Each service uses two [consumer groups](kafka.md#consumer-groups) shared by its buses, for commands and requests, and two for each bus, for events and responses, named after the bus's instance ID. That's a new GUID each time a bus starts, unless it's set with `UseInstanceId(...)`, so by default every restart adds two groups, and the old ones stay until they're removed. Microsoft doesn't document when Event Hubs removes unused Kafka consumer groups, so a service that restarts often, or runs many instances, can approach the limit.

To stay well within it, give each bus a fixed instance ID of its own with `UseInstanceId(...)`, such as the pod name in a Kubernetes StatefulSet, so a restarted bus reuses its groups. It then also receives the events published while it was stopped.
