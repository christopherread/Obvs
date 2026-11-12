# Obvs.Testing

An in-memory broker and test harness for [Obvs](https://github.com/christopherread/Obvs): test services end to end without a broker or fixed delays.

```bash
dotnet add package Obvs.Testing
```

`UseInMemory()` carries a service's messages in memory, and `ObvsTestHarness` starts a bus around it, and waits for the messages you expect:

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

For requests, time and contract snapshots, see [Testing](https://github.com/christopherread/Obvs/blob/master/docs/testing.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme).
