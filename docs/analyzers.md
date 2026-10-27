# Analyzers

The `Obvs` package includes analyzers that report common mistakes as you type, before the bus starts. They're on by default.

All rules are warnings. To change a rule's severity, set it in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.OBVS005.severity = error
```

The bus checks its configuration again when it starts (see [configuration errors](configuration-errors.md)). The analyzers catch what they can see in your code earlier.

<a id="obvs001"></a>
## OBVS001: a message registered or handled on a service doesn't implement its message interface

```csharp
obvs.AddService<IOrderMessage>("orders", service => service
    .UseRabbitMq(...)
    .AddMessages(typeof(ShipOrder))                                        // OBVS001
    .HandleCommand<ShipOrder>((command, context, ct) => Task.CompletedTask)); // OBVS001
```

A service sends and receives only messages that implement its message interface, here `IOrderMessage`. So it never receives `ShipOrder`, and the bus fails to start.

**Fix:** make the message implement the interface. The code fix does this when the message is in your code. Or, if the message belongs to another service, register it there.

<a id="obvs002"></a>
## OBVS002: a message is more than one kind of message

```csharp
public class PlaceAndAnnounce : IOrderMessage, ICommand, IEvent { } // OBVS002
```

A message is exactly one of `ICommand`, `IEvent`, `IRequest` or `IResponse`, and each kind travels its own way: commands to the service, events to every subscriber, requests and responses between a client and a server. A message that's two kinds would be sent both ways.

This includes a kind inherited from a base class: `class PlacedCommand : OrderPlaced, ICommand`.

**Fix:** split it into one message of each kind. For example, a `PlaceOrder` command whose handler publishes an `OrderPlaced` event.

<a id="obvs003"></a>
## OBVS003: a request handler's response doesn't belong to the service

```csharp
obvs.AddService<IOrderMessage>("orders", service => service
    .UseRabbitMq(...)
    .HandleRequest<GetQuote, ShippingQuote>(...)); // OBVS003: ShippingQuote is an IShippingMessage
```

Responses go back over the service the request came in on. A response that doesn't implement the service's message interface is sent where no client of this service listens, so the request times out.

**Fix:** make the response implement the service's message interface.

<a id="obvs004"></a>
## OBVS004: a service has no transport

```csharp
obvs.AddService<IOrderMessage>("orders", service => service
    .HandleCommand<PlaceOrder, PlaceOrderHandler>()); // OBVS004
```

A service needs a transport to send and receive its messages. Without one, the bus fails to start.

**Fix:** call one on the service: `UseRabbitMq`, `UseKafka`, `UseNats`, `UseNatsJetStream`, `UseActiveMq`, `UseAzureServiceBus` or `UseAmazonSqs`, or `UseInMemory` in tests. The code fix offers each transport your project references, and adds it with empty options: the bus's startup checks then say which options it needs.

<a id="obvs005"></a>
## OBVS005: the subscription to a bus stream is discarded

```csharp
bus.Events.OfType<OrderPlaced>().Subscribe(placed => Ship(placed)); // OBVS005
```

`Subscribe` returns the subscription. Without it, the subscription can't be stopped, and it keeps receiving until the app ends.

**Fix:** keep the subscription, and dispose it when you're done. The code fix stores it in a local. Better still, for work the app does as long as it runs, use a handler: `HandleCommand`, `HandleEvent` or `HandleRequest`. Handlers start and stop with the host, and settle each message once it's handled.

<a id="obvs006"></a>
## OBVS006: an async lambda subscribes to a bus stream

```csharp
var subscription = bus.Events.OfType<OrderPlaced>()
    .Subscribe(async placed => await ShipAsync(placed)); // OBVS006
```

`Subscribe` takes an `Action<T>`, so an async lambda compiles to `async void`. Each message starts its own run without waiting for the last, so messages are handled at the same time and out of order. An exception can't reach the subscription, so it crashes the app.

**Fix:** use a handler (`HandleCommand`, `HandleEvent` or `HandleRequest`). It awaits each message, takes `MaxConcurrency` for how many at once, and logs and traces failures. Or the code fix runs the lambda for each message in turn, so an exception ends the subscription through `OnError`:

```csharp
var subscription = bus.Events.OfType<OrderPlaced>()
    .Select(placed => Observable.FromAsync(async () => await ShipAsync(placed)))
    .Concat()
    .Subscribe();
```
