# Kafka

`Obvs.Kafka` carries a service's messages over Kafka. Each service gets four topics, `<service>.Commands`, `.Events`, `.Requests` and `.Responses`, created at startup if they don't exist.

<!-- snippet: kafka-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"))));
```

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": { "BootstrapServers": "localhost:9092" }
    }
  }
}
```

## Managed platforms

Each platform has its own page with the settings it needs:

- [Confluent Cloud](kafka-confluent-cloud.md)
- [Amazon MSK](kafka-amazon-msk.md)
- [Azure Event Hubs](kafka-azure-event-hubs.md)

## Security settings

These are all in `KafkaOptions`, and can be set from configuration or in code. They apply to every Kafka client the bus creates for the service: its producers, its consumers and the admin client that creates its topics.

| Setting | What it's for |
|---|---|
| `SecurityProtocol` | `PLAINTEXT` (the default), `SSL`, `SASL_PLAINTEXT` or `SASL_SSL` |
| `SaslMechanism` | `PLAIN`, `SCRAM-SHA-256`, `SCRAM-SHA-512` or `OAUTHBEARER` |
| `SaslUsername`, `SaslPassword` | Credentials for `PLAIN` and `SCRAM` |
| `OAuthBearerTokenProvider` | Gets tokens for `OAUTHBEARER`. Code only, not configuration |
| `SslCaLocation`, `SslCaPem` | CA certificates for the brokers' certificates. The operating system's trusted CAs are used when these aren't set, which suits the managed platforms |
| `SslCertificateLocation`, `SslKeyLocation`, `SslKeyPassword` | A client certificate, for brokers that authenticate clients with TLS |
| `EnableSslCertificateVerification` | Turn off only for a local broker with a self-signed certificate |

Names can be written as Kafka writes them (`SASL_SSL`, `SCRAM-SHA-512`), so settings copied from a platform's docs work as they are. Confluent.Kafka's enum names (`SaslSsl`, `ScramSha512`) work too.

Keep secrets such as `SaslPassword` out of `appsettings.json`: use user secrets locally, and environment variables or a key vault when deployed. For example, the environment variable `Obvs__Orders__Kafka__SaslPassword` sets `Obvs:Orders:Kafka:SaslPassword`.

### OAUTHBEARER tokens

`OAuthBearerTokenProvider` is called when each Kafka client starts, and again before each token expires. It returns a `KafkaOAuthBearerToken` with the token and its expiry. It can't come from configuration, so with the other settings in configuration, use the overload that takes both:

<!-- snippet: kafka-oauth -->
```csharp
service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
    options.OAuthBearerTokenProvider = async cancellationToken =>
    {
        var token = await GetTokenAsync(cancellationToken);
        return new KafkaOAuthBearerToken(token.Value, token.ExpiresOn);
    });
```

If the provider throws, the bus doesn't start, and the error includes the provider's message.

## Startup checks

The bus checks the settings when it starts, and reports every problem together in one `ObvsConfigurationException`, saying which setting to change. For example:

- a `SaslMechanism` without a `SASL_*` protocol, or a `SASL_*` protocol without a mechanism;
- `PLAIN` or `SCRAM` without both a user name and password, or `OAUTHBEARER` without a token provider;
- credentials or a token provider that the chosen mechanism wouldn't use;
- TLS settings without `SSL` or `SASL_SSL`, or a certificate file that doesn't exist;
- settings a platform is known to need: port 9093 and `SASL_SSL` on Event Hubs, `SASL_SSL` on Confluent Cloud, and the right mechanism for each Amazon MSK port.

When the brokers refuse to sign the bus in, or the TLS handshake fails, startup fails straight away with the broker's reason. It doesn't wait out the connection timeout.

## Consumer groups

Each topic is read in a consumer group of its own, named after the topic and not after the machine or process, so a restarted bus reuses its groups and carries on where they left off:

| Messages | Consumer group | Received by |
|---|---|---|
| Commands | `<service>.Commands`, shared by the service's buses | One of the service's buses |
| Requests | `<service>.Requests`, shared by the service's buses | One of the service's buses |
| Events | `<service>.Events.<instance ID>`, one per bus | Every bus |
| Responses | `<service>.Responses.<instance ID>`, one per bus | Every bus, which keeps the responses to its own requests |

- Kafka gives each partition of a topic to one member of a group, so commands and requests are spread across the service's buses only as far as their topics have partitions. With one partition, one bus handles them all, and another takes over if it stops. Topics created at startup get the broker's default number of partitions; to spread the work over more buses, create the topics beforehand with more.
- A group that's new starts at the end of its topic. After that, a bus carries on from the group's committed position, so commands and requests sent while none of the service's buses was running are handled when one starts.
- Positions are committed every few seconds as messages are received, not after they're handled. A bus that crashes can receive the last few messages again, or lose one it was handling.

The instance ID is the bus's `InstanceId`, a new GUID each time the bus starts unless it's set with `UseInstanceId(...)`. With a new one, a bus starts with new events and responses groups, at the end of the topics, so it doesn't receive events published while it was stopped. To keep its groups across restarts, give each bus a fixed instance ID of its own, such as the pod name in a Kubernetes StatefulSet:

<!-- snippet: kafka-instance-id -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .UseInstanceId(Environment.GetEnvironmentVariable("POD_NAME"))
    .AddService<IOrderMessage>("orders", service => service
        .UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"))));
```

Two running buses mustn't share an instance ID: Kafka would split the events between them.

### Groups left behind

A bus without a fixed instance ID leaves its events and responses groups behind when it stops. Kafka removes a group once it's empty and its committed positions have expired, `offsets.retention.minutes` after its last member left: seven days by default. Until then the groups count towards any limit on consumer groups, such as [Event Hubs'](kafka-azure-event-hubs.md#consumer-groups). To remove them sooner, delete them with Kafka's `kafka-consumer-groups.sh --bootstrap-server <brokers> --delete --group <group>`.

Services configured with v6's `WithKafkaEndpoints<...>()` keep v6's consumer groups, named after the machine and process, so every process receives every message, commands included.

## Errors while running

The Kafka client recovers by itself from most errors, such as brokers that are down, connections the platform closed, or a failed OAUTHBEARER token refresh, where it keeps the token it has and tries again. The bus logs those as warnings, in the `Obvs.Kafka` category, and carries on. A message the Kafka client can't deserialize, such as one another producer wrote to the topic in its own format, is logged and skipped.

These end the subscription instead, with the client's error:

- errors the client reports as fatal (`Error.IsFatal`), after which it stops consuming;
- being refused access to a topic, a consumer group or the cluster, which won't fix itself.

## Permissions

At startup the bus creates the service's topics if they don't exist. That needs permission to create topics; without it, create them beforehand. The service's consumer groups are all named after it, starting with `<service>.`, so on platforms with access control, grant read access to consumer groups with that prefix.

## Trying it locally

`Obvs.Kafka.Tests/SaslBroker/docker-compose.yml` runs a Kafka broker that requires SASL sign-in, on port 9094, with the user `obvs` and password `obvs-secret`:

```bash
docker compose -f Obvs.Kafka.Tests/SaslBroker/docker-compose.yml up -d
```

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "127.0.0.1:9094",
        "SecurityProtocol": "SASL_PLAINTEXT",
        "SaslMechanism": "PLAIN",
        "SaslUsername": "obvs",
        "SaslPassword": "obvs-secret"
      }
    }
  }
}
```
