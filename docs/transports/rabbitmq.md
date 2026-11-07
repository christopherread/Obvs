# RabbitMQ

`Obvs.RabbitMQ` carries a service's messages over RabbitMQ. Commands and requests go to quorum queues the service's servers share, so each one is handled once, and stays on its queue until it's handled. Events go to every bus, each through a queue of its own.

<!-- snippet: rabbitmq-setup -->
```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))));
```

```json
{ "Obvs": { "Orders": { "RabbitMq": { "Uri": "amqp://localhost:5672" } } } }
```

Quorum queues need RabbitMQ 3.8 or later. The tests run against RabbitMQ 4.4.

## How messages map to RabbitMQ

For a service named `orders`:

| Obvs | RabbitMQ |
|---|---|
| Every message | Published to the topic exchange `orders`, with the routing key `orders.{Kind}.{Type}`, for example `orders.Commands.PlaceOrder` |
| Commands | Quorum queue `orders.Commands`, shared by the service's servers |
| Requests | Quorum queue `orders.Requests`, shared by the service's servers |
| Events | Each bus has its own queue, which gets every event |
| Responses | Each bus has its own queue, which gets every response. The bus keeps those to its own requests |
| Headers and trace context | AMQP headers |
| Dead letters | The exchange `orders.DeadLetters`, and a quorum queue of the same name that keeps what's sent there |

A bus declares the exchanges and queues it needs. A server declares the commands and requests queues as it starts. Clients don't, as the service's servers may still be v6 apps, which don't read them: they'd fill up, and a v7 server would later handle everything on them again. So once a server has started, commands sent while none is running wait on the queue, but ones sent before any server has ever started are dropped.

Each bus's own queues for events and responses are deleted when it disconnects. Events published while a bus isn't running don't reach it.

## Delivery and settlement

Commands and requests are delivered at least once:

- A message stays on its queue until its handler succeeds. If the bus stops or crashes first, the message is delivered again, to another server or the same one once it's back. Handlers must tolerate duplicates.
- A message whose handler fails is delivered again, up to `MaxDeliveryAttempts` times. On the last attempt, the bus sends it to `orders.DeadLetters`, with its routing key and these headers:

  | Header | Value |
  |---|---|
  | `obvs-dead-letter-reason` | `HandlerFailed`, or `Undeserializable` for a message the bus couldn't read |
  | `obvs-dead-letter-description` | The exception's type and message |
  | `obvs-exception-type` | The exception's type |
  | `obvs-delivery-count` | How many times it was delivered |

- The queues' own delivery limit is `MaxDeliveryAttempts` too, as a backstop.
- A message the bus can't read is delivered again in the same way, as another server may be able to during a rolling deployment, then dead-lettered.
- A message of a type the service doesn't have is removed, with a warning in the log. A newer version of the service may send one.

Events and responses are acknowledged as they arrive, as in v6. A failing event handler doesn't get its event again.

RabbitMQ gives a message back to the queue if a handler holds it for longer than the broker's `consumer_timeout`, 30 minutes by default.

## Settings

All of these are in `RabbitMqOptions`, and can be set from configuration or in code.

| Setting | Default | What it's for |
|---|---|---|
| `Uri` | Required | The broker, for example `amqp://user:password@localhost:5672/vhost` |
| `AutomaticRecoveryEnabled` | `true` | Whether the connection reconnects and restores its channels after a network failure |
| `MaxDeliveryAttempts` | 10 | How many times a command or request is delivered to a failing handler before it's dead-lettered |
| `MaxConcurrentMessages` | The handlers' | How many commands, and how many requests, the bus takes at once. When not set, the sum of the `MaxConcurrency` of the service's handlers of them, or 1 if none takes them |

`MaxConcurrentMessages` is the channel's prefetch count: the broker holds back the rest for the service's other servers. Messages arrive in order only one at a time. For example, to handle up to 8 commands at once, set the handler's `MaxConcurrency`, which the commands queue follows:

<!-- snippet: rabbitmq-concurrency -->
```csharp
service
    .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))
    .HandleCommand<PlaceOrder>(PlaceOrderAsync, handler => handler.MaxConcurrency = 8);
```

Messages processed with `SubscribeAsync` or Rx aren't visible to the bus at startup: set `MaxConcurrentMessages` for those.

### Changing `MaxDeliveryAttempts`

`MaxDeliveryAttempts` is also the queues' `x-delivery-limit`, which RabbitMQ fixes when it creates them. After changing it, the bus reports that the queues exist with other settings, and doesn't receive from them. Delete `orders.Commands` and `orders.Requests`, moving any messages you need first, with a shovel for example, and the next server to start declares them again. The same goes for a queue someone else created with those names.

## Upgrading from v6

v6 gives every bus its own queue for commands and requests, so every instance of a service handles every one. v7 apps still use the same exchange and routing keys, so v6 and v7 apps send each other commands, requests, events and responses. But while v6 and v7 servers of one service run together, each command and request is handled by every v6 server, and by one of the v7 servers. Upgrade a service's servers together, or make sure their handlers tolerate it.

If you roll a service's servers back to v6, delete `orders.Commands` and `orders.Requests`: nothing reads them, and the next v7 server would handle what they kept.

## Not yet checked

- Against CloudAMQP and Amazon MQ, including whether their plans support quorum queues.
- Against a RabbitMQ cluster. Quorum queues are replicated across one, but the tests run against a single node.

## Trying it locally

```bash
docker run -d -p 5672:5672 rabbitmq:4.4-alpine
```

The transport's broker tests carry the `Explicit` trait and run against it. Set `RABBITMQ_URI` to use another broker.

```bash
dotnet test --project Obvs.RabbitMQ.Tests -- --filter-trait "Category=Explicit"
```
