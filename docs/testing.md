# Testing with Obvs.Testing

`Obvs.Testing` runs your services against an in-memory broker, in the test process. A test then waits for the messages it expects, which arrive as soon as they're sent, so there are no fixed delays.

```
dotnet add package Obvs.Testing
```

## A test

<!-- snippet: test-a-handler -->
```csharp
[Fact]
public async Task PlacingAnOrderPublishesOrderPlaced()
{
    await using var harness = await ObvsTestHarness.StartAsync(services => services
        .AddSingleton<IOrderStore, InMemoryOrderStore>()
        .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
            .UseInMemory()
            .HandleCommand<PlaceOrder, PlaceOrderHandler>())));

    await harness.Client.SendAsync(new PlaceOrder("o-1"));

    var placed = await harness.Published<OrderPlaced>(e => e.OrderId == "o-1");
    Assert.Equal("o-1", placed.OrderId);
}
```

`ObvsTestHarness.StartAsync` builds and starts a host with your services, and configures the bus as it is configured in your app. Replace each service's transport with `UseInMemory()`. All the services in a harness share its broker, so one service's handlers receive what another sends.

Disposing the harness stops the host: handlers finish what they're running, then the bus is disposed.

## Waiting for messages

| Method | Waits for |
|---|---|
| `Sent<TCommand>(predicate)` | a command to be sent |
| `Published<TEvent>(predicate)` | an event to be published |
| `Requested<TRequest>(predicate)` | a request to be sent |
| `Replied<TResponse>(predicate)` | a response to be sent |

Each returns as soon as a matching message passes through the broker. A message that passed through before the call counts too, so it doesn't matter whether the handler or the assertion runs first.

Each fails after five seconds by default. Pass a `timeout` to one call, or set `DefaultTimeout` for the harness. The failure, an `ObvsTestTimeoutException`, lists every message that did pass through the broker. Records print their values, which is usually enough to see what went wrong:

```
No matching OrderPlaced was published within 00:00:05. Messages that passed through the broker (2):
- PlaceOrder { OrderId = o-1 }
- OrderRejected { OrderId = o-1, Reason = Out of stock }
```

`harness.Messages` is every message so far, in order. Use it to show that something wasn't sent.

## Requests

`RequestAsync` works as it does against a real broker. The in-memory transport correlates each response with its request.

<!-- snippet: test-a-request -->
```csharp
var quote = await harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromSeconds(5));
```

## Time

<!-- snippet: test-with-time -->
```csharp
var time = new FakeTimeProvider();
await using var harness = await ObvsTestHarness.StartAsync(
    services => services.AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service.UseInMemory())),
    options =>
    {
        options.TimeProvider = time;             // the harness's timeouts
        options.Scheduler = new TestScheduler(); // where the broker delivers, for Rx's virtual time
    });
```

- **`TimeProvider`:** with a `FakeTimeProvider`, a test advances time to make a `Published<T>` time out, instead of waiting for it.
- **`Scheduler`:** with Rx's `TestScheduler`, the broker delivers only when the test advances the scheduler.

## Without the harness

`UseInMemory()` works in any host, and with [`ObvsBus.StartAsync`](../Obvs/Hosting/ObvsBus.cs) in a script. Pass your own `InMemoryBroker` to `UseInMemory(broker)` to share it between hosts, for example to run two apps in one test.
