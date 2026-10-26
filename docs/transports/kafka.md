# Kafka

`Obvs.Kafka` carries a service's messages over Kafka. Each service gets four topics, `<service>.Commands`, `.Events`, `.Requests` and `.Responses`, created at startup if they don't exist.

```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"))
        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)))));
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

## Permissions

At startup the bus creates the service's topics if they don't exist. That needs permission to create topics; without it, create them beforehand. Each bus also uses consumer groups of its own, named after the machine and process, so on platforms with access control, grant read access to consumer groups by any name, not one fixed name.

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
