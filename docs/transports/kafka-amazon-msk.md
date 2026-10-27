# Kafka on Amazon MSK

Amazon MSK listens on a different port for each kind of access control the cluster has turned on. Use the bootstrap servers the MSK console's **View client information** shows for the one you want:

| Access control | Port (private) | Port (public access) | Settings |
|---|---|---|---|
| IAM | 9098 | 9198 | `SASL_SSL`, `OAUTHBEARER` and a token provider |
| SASL/SCRAM | 9096 | 9196 | `SASL_SSL`, `SCRAM-SHA-512`, a user name and password |
| TLS | 9094 | 9194 | `SSL`, and a client certificate if the cluster authenticates clients with TLS |

The startup checks know these ports: settings that don't match the port stop the bus starting, with an error saying what that port needs. MSK Serverless supports IAM only, on port 9098.

> Obvs's tests run against a local Kafka broker with SASL, not against Amazon MSK itself.

## IAM

The MSK IAM token is signed with your AWS credentials, using AWS's signer package, [`AWS.MSK.Auth`](https://www.nuget.org/packages/AWS.MSK.Auth). It finds credentials the usual way: an IAM role, a profile or environment variables.

```bash
dotnet add package AWS.MSK.Auth
```

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098,b-2.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9098",
        "SecurityProtocol": "SASL_SSL",
        "SaslMechanism": "OAUTHBEARER"
      }
    }
  }
}
```

```csharp
using Amazon;
using AWS.MSK.Auth;

var tokenGenerator = new AWSMSKAuthTokenGenerator();

service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"), options =>
    options.OAuthBearerTokenProvider = cancellationToken =>
    {
        var (token, expiryMs) = tokenGenerator.GenerateAuthToken(RegionEndpoint.USEast1);
        return Task.FromResult(new KafkaOAuthBearerToken(token, DateTimeOffset.FromUnixTimeMilliseconds(expiryMs)));
    });
```

The role or user needs an IAM policy allowing, on the cluster, its topics and its groups:

- `kafka-cluster:Connect`
- `kafka-cluster:DescribeTopic`, `kafka-cluster:CreateTopic` (or create the topics beforehand)
- `kafka-cluster:WriteData`, `kafka-cluster:ReadData`
- `kafka-cluster:DescribeGroup`, `kafka-cluster:AlterGroup` on `group/<cluster>/orders.*`, which covers all the service's [consumer groups](kafka.md#consumer-groups).

The Java client's `AWS_MSK_IAM` mechanism isn't available in .NET. `OAUTHBEARER` with the signer is the .NET equivalent.

## SASL/SCRAM

MSK keeps SCRAM credentials in AWS Secrets Manager, in a secret whose name starts with `AmazonMSK_`, associated with the cluster. MSK supports only `SCRAM-SHA-512`.

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9096,b-2.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9096",
        "SecurityProtocol": "SASL_SSL",
        "SaslMechanism": "SCRAM-SHA-512",
        "SaslUsername": "orders",
        "SaslPassword": "<from the AmazonMSK_ secret>"
      }
    }
  }
}
```

```csharp
service.UseKafka(builder.Configuration.GetSection("Obvs:Orders:Kafka"));
```

Read the password from Secrets Manager into configuration, for example with a configuration provider, rather than putting it in `appsettings.json`. With SCRAM, MSK authorises with Kafka ACLs, so the user needs `CREATE` (or topics created beforehand), `READ` and `WRITE` on the topics, and `READ` on consumer groups with the prefix `orders.`.

## TLS

```json
{
  "Obvs": {
    "Orders": {
      "Kafka": {
        "BootstrapServers": "b-1.orders.abc123.c2.kafka.us-east-1.amazonaws.com:9094",
        "SecurityProtocol": "SSL",
        "SslCertificateLocation": "/etc/obvs/client.pem",
        "SslKeyLocation": "/etc/obvs/client.key"
      }
    }
  }
}
```

Leave out the certificate and key if the cluster doesn't authenticate clients by certificate. MSK's broker certificates are signed by a public CA, so `SslCaLocation` isn't needed.
