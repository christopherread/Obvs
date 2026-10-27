# Amazon SQS and SNS

`Obvs.AmazonSqs` carries a service's messages over Amazon SQS and SNS. Commands and requests go to SQS queues the service's buses share, so each one is handled once. Events go to an SNS topic, which delivers them to a queue for each bus. The queues and topic are created at startup if they don't exist.

<!-- snippet: sqs-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"))));
```

```json
{
  "Obvs": {
    "Orders": {
      "AmazonSqs": { "Region": "eu-west-1" }
    }
  }
}
```

Or in code:

<!-- snippet: sqs-region -->
```csharp
service.UseAmazonSqs(options => options.Region = "eu-west-1");
```

## How messages map to SQS and SNS

For a service named `orders`, on a bus with the instance ID `worker-1`:

| Obvs | AWS |
|---|---|
| Commands | SQS queue `orders-commands`, shared by the service's buses |
| Requests | SQS queue `orders-requests`, shared by the service's buses |
| Events | SNS topic `orders-events`. Each bus has its own SQS queue, `orders-events-worker-1`, subscribed to the topic with raw message delivery, so every bus gets every event |
| Responses | Each bus has its own SQS queue, `orders-responses-worker-1`. A request carries its bus's reply queue URL in the `obvs-reply-to` attribute, and the response is sent there |
| Dead letters | SQS queue `orders-dead-letters`, shared by the service's queues. Messages are kept for 14 days, the longest SQS allows |

The instance ID comes from `UseInstanceId(...)`, or is generated for each run when not set. A bus deletes its own two queues, and unsubscribes from the topic, when it stops. A bus that crashes leaves them behind: see [Cleaning up after crashed buses](#cleaning-up-after-crashed-buses).

Settings such as the visibility timeout and redrive policy apply only to queues Obvs creates. Queues that already exist keep theirs.

The queues are standard queues: messages can arrive out of order, and occasionally more than once, so handlers must tolerate duplicates. FIFO queues and topics aren't supported yet.

## Settings

All of these are in `AmazonSqsOptions`. Everything except `Credentials` can be set from configuration or in code.

| Setting | Default | What it's for |
|---|---|---|
| `Region` | Required | The AWS region of the queues and topic, for example `eu-west-1` |
| `ServiceUrl` | AWS's own | An endpoint to use instead of AWS's, for example `http://localhost:4566` for LocalStack |
| `Credentials` | The AWS SDK's default chain | An `AWSCredentials` to sign in with. Code only |
| `MaxDeliveryAttempts` | 10 | How many times a message is delivered to a failing handler before it's dead-lettered, from 1 to 1000 |
| `MaxConcurrentCalls` | 1 | How many messages each queue hands this bus at once. Raise it together with a handler's `MaxConcurrency` |
| `VisibilityTimeout` | 1 minute | How long a received message is hidden from other receivers, from 1 second to 12 hours. It must exceed `MaxHandlerDuration` |
| `MaxHandlerDuration` | 30 seconds | The longest a handler is expected to take with a message |
| `CleanUpAbandonedQueues` | true | Whether buses delete the queues crashed buses left behind |
| `LastSeenInterval` | 5 minutes | How often a bus records that it's still running, and looks for crashed buses' queues |
| `AbandonedAfter` | 1 hour | How long a bus can go unseen before other buses delete its queues. At least three times `LastSeenInterval` |

For example, to handle up to 8 messages at once with handlers that can take up to 4 minutes:

```json
{
  "Obvs": {
    "Orders": {
      "AmazonSqs": {
        "Region": "eu-west-1",
        "MaxConcurrentCalls": 8,
        "VisibilityTimeout": "00:05:00",
        "MaxHandlerDuration": "00:04:00",
        "MaxDeliveryAttempts": 5
      }
    }
  }
}
```

<!-- snippet: sqs-concurrency -->
```csharp
service
    .UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"))
    .HandleCommand<PlaceOrder>(PlaceOrderAsync, handler => handler.MaxConcurrency = 8);
```

### Credentials

By default the bus signs in with the AWS SDK's default credential chain: environment variables, the shared credentials and config files, and the IAM role of the EC2 instance, ECS task or Lambda function it runs in. To sign in some other way, set `Credentials` in code. With the other settings in configuration, call `UseAmazonSqs` with the configuration section, then `services.Configure<AmazonSqsOptions>("orders", ...)` with the service's name:

<!-- snippet: sqs-credentials -->
```csharp
service.UseAmazonSqs(builder.Configuration.GetSection("Obvs:Orders:AmazonSqs"));
builder.Services.Configure<AmazonSqsOptions>("orders", options => options.Credentials = new BasicAWSCredentials(accessKey, secretKey));
```

Keep access keys out of `appsettings.json`. When the bus runs in AWS, prefer an IAM role to keys.

### Visibility timeout

SQS doesn't know a handler is still running. If a handler takes longer than `VisibilityTimeout`, SQS delivers the message again, to this bus or another, while the first handler is still at it. Set `MaxHandlerDuration` to the longest a handler takes, and `VisibilityTimeout` above it. The bus won't start if the visibility timeout doesn't exceed the handler duration.

## Startup checks

The bus checks the settings when it starts, and reports every problem together in one `ObvsConfigurationException`, saying which setting to change. It checks that:

- a region is set, and `ServiceUrl`, when set, is an `http` or `https` URL;
- `MaxDeliveryAttempts` is from 1 to 1000, and `MaxConcurrentCalls` is at least 1;
- `VisibilityTimeout` is from 1 second to 12 hours, and exceeds `MaxHandlerDuration`;
- with clean-up on, `LastSeenInterval` is positive and `AbandonedAfter` is at least three times it;
- the service name and instance ID use only letters, digits, hyphens and underscores, and the bus's queue names fit in SQS's 80 characters.

## Settlement and dead-lettering

A message is settled after its handler finishes:

| Outcome | What happens in SQS |
|---|---|
| The handler succeeds | The message is deleted |
| The handler fails | The message is made visible again straight away, so it's delivered again |
| The handler fails on the message's last delivery | The message is sent to `orders-dead-letters`, then deleted from its queue |
| The bus stops before the handler finishes | The message is delivered again once its visibility timeout passes |
| No handler takes the message, or its type isn't one of the service's | The message is deleted |

The last delivery is the `MaxDeliveryAttempts`th, counted by SQS's `ApproximateReceiveCount`. A dead-lettered message keeps its body and attributes, with these added:

| Attribute | Value |
|---|---|
| `obvs-dead-letter-reason` | `HandlerFailed` |
| `obvs-dead-letter-description` | The exception's message, cut to 4096 characters |
| `obvs-exception-type` | The exception's full type name |
| `obvs-delivery-attempts` | How many times the message was delivered |

Every queue Obvs creates also has a redrive policy that moves a message to the dead-letter queue once it's been received `MaxDeliveryAttempts` times. That catches messages Obvs can't settle itself, such as those that can't be deserialized, which are made visible again until the redrive policy moves them. Those arrive without the attributes above.

To look at or replay dead letters, use the SQS console's dead-letter queue redrive, or receive from `orders-dead-letters` yourself.

## Headers and the size limit

SQS and SNS allow 10 message attributes on a message, so the bus packs its own into a few:

| Attribute | Value |
|---|---|
| `TypeName` | The message's type name, which picks its deserializer |
| `obvs-headers` | Every Obvs header, including the W3C trace context and headers the app sets, as one JSON object |
| `obvs-reply-to` | On requests only, the URL of the reply queue |
| `obvs-body-encoding` | `base64` when the body isn't text SQS can carry, as from binary serializers such as ProtoBuf or MessagePack |

An app can set any number of headers, as long as the message fits.

SQS and SNS take at most 256 KiB, 262,144 bytes, in one message, counting the body and every attribute's name, type and value. A base64 body is a third bigger than the serialized message. Sending or publishing a message over the limit throws an `InvalidOperationException` that names the limit and the message's size. Storing large bodies in S3 isn't supported yet, so keep large payloads elsewhere and send a reference to them.

## Cleaning up after crashed buses

SQS never deletes a queue itself, so a bus that crashes leaves its event and reply queues behind, and the topic keeps sending every event to the crashed bus's event queue. Those messages stay there until the queue's retention period, 4 days by default, removes them.

To clean these up, each running bus records when it was last seen, and deletes the queues of buses that haven't been seen for a while:

- Each bus tags its own two queues with `obvs-last-seen`, the time in UTC, and `obvs-abandoned-after`, its `AbandonedAfter` in seconds. Alongside `obvs-instance-id` and `obvs-service`, these show in the SQS console.
- Every `LastSeenInterval`, the bus refreshes `obvs-last-seen`. It then lists the service's `-events-` and `-responses-` queues, and deletes any whose `obvs-last-seen` is older than its own `obvs-abandoned-after`. Before deleting an event queue, it unsubscribes it from the topic, so the topic stops sending to it.
- Only buses that are clients of the service do this, as only they have queues of their own. A service whose buses are all servers (`AsServer()`) has nothing to clean up.

With the defaults, a crashed bus's queues are gone within an hour and five minutes of its last refresh.

### Keeping a slow or paused bus's queues

- A bus is judged by the `AbandonedAfter` it recorded itself, not by the sweeping bus's setting, so buses of one service can be configured differently.
- `AbandonedAfter` must be at least three times `LastSeenInterval`, so missing a refresh or two, because AWS was briefly unreachable or the bus was busy, doesn't get a bus taken for crashed. The refresh runs on its own timer, apart from message handling.
- A sweeping bus reads the tags again just before deleting a queue, in case its bus has come back since.
- A bus never deletes its own queues, nor any queue that isn't another bus's own queue of the same service.
- A queue without both tags, or with tags that can't be read, is left alone.
- Time on each machine is compared with time on others, so allow for clock differences in `AbandonedAfter`. The default hour leaves plenty of room.

A paused bus can't be told apart from a crashed one. If a bus is paused for longer than its `AbandonedAfter`, for example in a debugger or on a suspended machine, other buses delete its queues. When it resumes, it recreates them and subscribes again at its next refresh, within `LastSeenInterval`, and goes on receiving: meanwhile it keeps trying to receive, rather than reporting the missing queues as errors. Events published while its queue was gone are lost to it, and responses sent to its reply queue meanwhile are lost too, so their requests time out.

### Several buses at once

Every client bus sweeps, so several may find the same crashed bus at once. Deleting a queue or subscription another bus has already deleted is ignored. Refreshes are spread between three quarters of `LastSeenInterval` and all of it, so buses started together don't stay in step.

Each sweep makes two `ListQueues` calls, one `ListQueueTags` call for each of the service's other buses' queues, and each refresh a few calls on the bus's own queues. With many buses, raise `LastSeenInterval` and `AbandonedAfter` together.

### Turning it off

Set `CleanUpAbandonedQueues` to `false`. The bus then neither records when it was last seen, removing the tags if its queues already have them, nor deletes other buses' queues. Buses with clean-up on leave its queues alone, so delete them yourself if it crashes: the `obvs-instance-id` and `obvs-service` tags make them easy to find.

## Permissions

The bus needs these permissions. In this example the service is `orders`, in `eu-west-1` and account `123456789012`. The bus's clients need the same permissions on the service's entities as its servers do, since every bus creates whatever's missing at startup.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ServiceQueues",
      "Effect": "Allow",
      "Action": [
        "sqs:CreateQueue",
        "sqs:GetQueueUrl",
        "sqs:GetQueueAttributes",
        "sqs:SetQueueAttributes",
        "sqs:TagQueue",
        "sqs:UntagQueue",
        "sqs:ListQueueTags",
        "sqs:DeleteQueue",
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:ChangeMessageVisibility"
      ],
      "Resource": "arn:aws:sqs:eu-west-1:123456789012:orders-*"
    },
    {
      "Sid": "FindCrashedBusesQueues",
      "Effect": "Allow",
      "Action": "sqs:ListQueues",
      "Resource": "*"
    },
    {
      "Sid": "ServiceTopic",
      "Effect": "Allow",
      "Action": [
        "sns:CreateTopic",
        "sns:Publish",
        "sns:Subscribe",
        "sns:ListSubscriptionsByTopic"
      ],
      "Resource": "arn:aws:sns:eu-west-1:123456789012:orders-events"
    },
    {
      "Sid": "Unsubscribe",
      "Effect": "Allow",
      "Action": "sns:Unsubscribe",
      "Resource": "*"
    }
  ]
}
```

What each is for:

- `CreateQueue`, `GetQueueUrl`, `GetQueueAttributes` and `CreateTopic` create the entities at startup, or find them. `GetQueueAttributes` also reads queue ARNs.
- `TagQueue` tags the bus's own queues, at startup and every `LastSeenInterval`. Creating a queue with tags needs it too.
- `SetQueueAttributes` sets the queue policy on the bus's event queue (below).
- `Subscribe` subscribes that queue to the topic, and `Unsubscribe` and `DeleteQueue` remove the bus's own when it stops.
- `ListQueues`, `ListQueueTags`, `ListSubscriptionsByTopic`, `Unsubscribe` and `DeleteQueue` clean up after crashed buses. Without clean-up, `ListQueues`, `ListQueueTags` and `ListSubscriptionsByTopic` aren't needed. `UntagQueue` is needed only with clean-up off.
- `SendMessage`, `ReceiveMessage`, `DeleteMessage`, `ChangeMessageVisibility` and `Publish` carry and settle messages. A server sends responses to each client bus's reply queue, and dead letters to `orders-dead-letters`.

`orders-*` also matches queues of any other service whose name starts with `orders-`. Name the queues one by one, as `orders-commands`, `orders-requests`, `orders-dead-letters`, `orders-events-*` and `orders-responses-*`, to avoid that.

If the queues and topic are created beforehand, by CloudFormation or Terraform for example, the bus finds them, and `CreateQueue` and `CreateTopic` are only needed for the bus's own queues.

### The queue policy for SNS

SNS can deliver to an SQS queue only if the queue's policy lets it. The bus sets this policy on its own event queue at startup:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Service": "sns.amazonaws.com" },
      "Action": "sqs:SendMessage",
      "Resource": "arn:aws:sqs:eu-west-1:123456789012:orders-events-worker-1",
      "Condition": { "ArnEquals": { "aws:SourceArn": "arn:aws:sns:eu-west-1:123456789012:orders-events" } }
    }
  ]
}
```

The policy replaces any the queue had. Obvs doesn't set a KMS key on the queues it creates. If you encrypt them, or the topic, with a customer managed KMS key, the key's policy must also let SNS use it.

## Not yet checked against AWS

The LocalStack tests cover the transport end to end, but LocalStack doesn't enforce IAM or queue policies, and doesn't emulate every AWS behaviour. These haven't been tried against AWS itself:

- the queue policy above, which lets SNS deliver to each bus's event queue;
- the IAM policy above, including whether `sns:Unsubscribe` can be scoped more tightly than `*`;
- retrying `CreateQueue` after `QueueDeletedRecently`. AWS refuses to create a queue for 60 seconds after one of the same name was deleted, as when a bus with a fixed instance ID restarts, or recreates its queues after being taken for crashed. The bus retries for up to 70 seconds;
- signing in with the AWS SDK's default credential chain, and reaching AWS's own endpoints, without `ServiceUrl`;
- clean-up across many queues: `ListQueues` paging beyond 1,000 queues, and `ListQueues` being eventually consistent.

## Trying it locally

[LocalStack](https://github.com/localstack/localstack) emulates SQS and SNS. `Obvs.AmazonSqs.Tests/LocalStack/docker-compose.yml` runs it on port 4566:

```bash
docker compose -f Obvs.AmazonSqs.Tests/LocalStack/docker-compose.yml up -d
```

The image is pinned to `localstack/localstack:4.14.0`, the last that runs without a LocalStack account: later images quit unless `LOCALSTACK_AUTH_TOKEN` is set.

Point the service at it with `ServiceUrl`. LocalStack accepts any credentials, but the SDK still needs some, so set them in code or with the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables:

```json
{
  "Obvs": {
    "Orders": {
      "AmazonSqs": {
        "Region": "us-east-1",
        "ServiceUrl": "http://localhost:4566"
      }
    }
  }
}
```

<!-- snippet: sqs-localstack -->
```csharp
service.UseAmazonSqs(options =>
{
    options.Region = "us-east-1";
    options.ServiceUrl = "http://localhost:4566";
    options.Credentials = new BasicAWSCredentials("test", "test");
});
```

The transport's broker tests carry the `Explicit` trait and run against LocalStack. Set `AWS_SERVICE_URL` to use another endpoint.

```bash
dotnet test --project Obvs.AmazonSqs.Tests -- --filter-trait "Category=Explicit"
```

To make clean-up easy to watch locally, shorten its timings, for example `LastSeenInterval` of 10 seconds and `AbandonedAfter` of 30 seconds, then kill a bus's process and watch another bus delete its queues.
