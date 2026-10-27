# Kafka on Confluent Cloud

Confluent Cloud clusters take connections over TLS with SASL sign-in. Sign in with an API key and secret, or with OAuth tokens from your identity provider.

> Obvs's tests run against a local Kafka broker with SASL, not against Confluent Cloud itself.

## API key and secret

Create an API key for the cluster in the Confluent Cloud console or with `confluent api-key create --resource <cluster-id>`. The bootstrap server is on the cluster's settings page.

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "pkc-abc12.us-east-1.aws.confluent.cloud:9092",
        "SecurityProtocol": "SASL_SSL",
        "SaslMechanism": "PLAIN",
        "SaslUsername": "<API key>",
        "SaslPassword": "<API secret>"
      }
    }
  }
}
```

```csharp
service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"));
```

Keep the secret out of `appsettings.json`: use user secrets locally, and an environment variable such as `Obvs__Orders__Kafka__SaslPassword` or a key vault when deployed.

Confluent Cloud's certificates are signed by public CAs, so leave the `Ssl*` settings unset.

## OAuth

To sign in with tokens from an identity provider configured in Confluent Cloud, use `OAUTHBEARER` with a token provider. Confluent Cloud needs the cluster ID and identity pool ID sent with each token, as SASL extensions:

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "pkc-abc12.us-east-1.aws.confluent.cloud:9092",
        "SecurityProtocol": "SASL_SSL",
        "SaslMechanism": "OAUTHBEARER"
      }
    }
  }
}
```

```csharp
service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
    options.OAuthBearerTokenProvider = async cancellationToken =>
    {
        // For example a client credentials grant against your identity provider
        var token = await identityProvider.GetTokenAsync(cancellationToken);
        return new KafkaOAuthBearerToken(token.AccessToken, token.ExpiresAt)
        {
            Extensions = new Dictionary<string, string>
            {
                ["logicalCluster"] = "lkc-abc12",
                ["identityPoolId"] = "pool-AbCd",
            },
        };
    });
```

## Permissions

At startup the bus creates its four topics (`orders.Commands`, `orders.Events`, `orders.Requests` and `orders.Responses`) if they don't exist, with the cluster's default partitions and replication. Give the API key's principal:

- `CREATE` on the topics, or create them beforehand;
- `READ` and `WRITE` on the topics;
- `READ` on consumer groups with the prefix `orders.`, which all the service's [consumer groups](kafka.md#consumer-groups) start with.

## Startup checks

A Confluent Cloud bootstrap server without `SASL_SSL`, or `PLAIN` without both a key and a secret, stops the bus starting, with an error naming the setting to change. A wrong key or secret fails at startup with the broker's reason.
