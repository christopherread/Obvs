# Obvs v7: an AI-native design

**Status:** draft, open for discussion
**Milestone:** [v7](https://github.com/christopherread/Obvs/milestone/2)

## Summary

More and more code that uses libraries like Obvs is written by AI coding agents, or with their help. v7 redesigns how Obvs is configured, consumed, tested and documented, so that an agent (and a person new to the library) can use it correctly the first time. When it does go wrong, the compiler, the tests or the error message should say how to fix it.

The core model stays the same: each service has typed commands, events, requests and responses, carried over swappable transports and serializers. What changes is the surface around it.

## Why "AI native"

Coding agents work in a loop: write code from patterns they have seen many times, compile, run the tests, read the output, and correct. A library fits that loop well when:

- **It looks like code they already know.** Standard .NET idioms (`AddX` on `IServiceCollection`, `IOptions`, `ILogger`, handlers, `async`/`await`) are reproduced reliably. Bespoke patterns are not.
- **Mistakes fail early and loudly.** A compile-time diagnostic beats a startup exception, which beats a runtime exception, which beats a message that silently goes nowhere.
- **Error text names the fix.** The agent's next edit is driven by the last error it read.
- **Tests are fast and deterministic.** Flaky, sleep-based tests send an agent chasing problems that aren't there.
- **The system can be inspected.** An agent that can ask "what messages does this service accept?" doesn't have to guess.
- **There is one obvious way.** Every extra option is another way to choose wrong.

These are also just good API design for people. Nothing here is specific to AI, but AI agents make the cost of getting it wrong much more visible.

## Where v6 makes this hard

| Area | v6 today | Effect on an agent |
|---|---|---|
| Configuration | A fluent chain with 5–6 generic type parameters (`ServiceBus.Configure().WithRabbitMQEndpoints<TServiceMessage>().Named(..).ConnectToBroker(..).SerializedAsJson().AsClientAndServer().Create()`), with no `Microsoft.Extensions` integration | Gets the call order wrong; the compiler errors are about generic constraints, not the actual mistake |
| Message discovery | Reflection scan of every assembly in the app domain (`MessageTypes.GetAllAssembliesFromAppDomain`), with assembly and type filters | A missed type only fails at runtime ("Unable to find deserializer"); breaks with trimming, AOT and probably single-file builds (#42) |
| Consuming messages | `IObservable<T>` only: `Subscribe`, `OfType`, `ObserveOn`; requests answered via `GetResponses(..).Subscribe(..)` | Subscriptions dropped or never disposed; replies sent from the wrong place; no `CancellationToken` anywhere |
| Errors | Late runtime exceptions, or pushed to the `Exceptions` observable, which is easy not to subscribe to | Failures are invisible, or point at the symptom rather than the cause |
| Testing | No test support; the library's own transport tests publish, then `Task.Delay` and assert | Agents copy the pattern and get slow, flaky tests |
| Observability | Separate packages for AppMetrics, Elasticsearch, Performance Counters and NLog | No traces to debug with; another set of APIs to learn |
| Packages | 17 packages, several thin or unmaintained | More ways to pick the wrong one |
| Docs | A README with examples that aren't compiled, and no XML docs in the packages | Examples drift; agents can't read the docs from the package |

## Proposed changes

Each change below has a tracking issue. Code sketches show the intended shape, not final API names.

### 1. Host the bus through dependency injection

Tracking: #84

Configuration moves to the standard .NET hosting model:

```csharp
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))
        .AddMessagesFrom<OrderMessages>()));
```

```json
{
  "Obvs": {
    "Orders": {
      "RabbitMq": { "Uri": "amqp://localhost:5672" }
    }
  }
}
```

- Per-service options bound from `IConfiguration`, and validated on start (`ValidateOnStart`).
- `ILogger` throughout; no logging adapter packages.
- An `IHostedService` starts the bus (connects, declares, subscribes) and stops it cleanly.
- `IServiceBus` and `IServiceBusClient` are resolvable from DI. Handlers are resolved from DI with a scope per message (see 4).
- The non-generic surface (`IServiceBus` over `IMessage`, `ICommand`, ...) is the default. The six-parameter generic variants stay available, but are no longer what the docs lead with.

#### Lifetime

The bus stays a single shared instance, as in v6. Dependency injection makes that explicit and enforced instead of a convention:

```csharp
services.TryAddSingleton<ServiceBus>(sp => /* build from options */);
services.TryAddSingleton<IServiceBus>(sp => sp.GetRequiredService<ServiceBus>());       // same instance,
services.TryAddSingleton<IServiceBusClient>(sp => sp.GetRequiredService<ServiceBus>()); // not a second bus
services.AddHostedService<ObvsHostedService>();
```

- **One instance behind both interfaces.** `IServiceBus` and `IServiceBusClient` forward to the same registration. Registering each separately is a classic DI mistake that would create two buses.
- **The container owns it.**
  - The hosted service starts it when the app starts: connecting, declaring and subscribing before the app takes traffic, and surfacing configuration errors immediately (see 6).
  - On shutdown it stops the bus, letting in-flight handlers finish.
  - The container disposes it. Application code no longer disposes the bus, unlike v6's `((IDisposable)serviceBus).Dispose()`.
- **Handlers are scoped, the bus is not.** Each message's handler is resolved in its own DI scope, so handlers can depend on scoped services such as a `DbContext`. The bus never holds onto a scoped service.
- **`AddObvs` is safe to call more than once.** Repeated calls add to the same bus configuration, or fail with an actionable error on conflicts. They never create a second bus by accident.
- **Singleton means one per container, not one per process.** Each `WebApplicationFactory`, test harness (see 5) or extra host in a process builds its own container, and therefore its own bus. That's what tests want, but v6 derives broker-side names from the *process*, so two buses in one process collide:

  | Transport | v6 naming | Two buses in one process |
  |---|---|---|
  | RabbitMQ | Exclusive queues named host-user-**process ID**-subscriber count | The second bus declares the same exclusive queue name and fails |
  | Kafka | `GroupId` = host-user-**process ID** | Both buses join one consumer group and Kafka splits the messages between them, so each silently misses some |
  | ActiveMQ | Client ID from the process, plus the Windows user on .NET Framework | Duplicate client ID on net472, so the second connection is refused |
  | Request/response | `RequesterId` = process name-host-user-process ID | Harmless: responses are also matched by request ID |

  v7 gives each bus an **instance ID**: a GUID by default, overridable in options for stable names across restarts where a transport needs them. Every broker-side name uses it instead of the process ID. Calling v6's `.Create()` twice in one process hits the same collisions; DI just makes it much more likely.
- **Several buses on purpose.** This is rarely needed, since one bus already hosts many services across different transports. Where it is, keyed services are supported, with the unkeyed bus as the default:

  ```csharp
  services.AddObvs("analytics", obvs => ...);
  public sealed class Reporter([FromKeyedServices("analytics")] IServiceBus analyticsBus) { ... }
  ```

- **No host required.** Scripts and small console tools can use a lightweight builder that wraps a private container (`await using var bus = await ObvsBus.StartAsync(obvs => ...)`), with the same lifetime rules.

### 2. Explicit, generated message registration

Tracking: #85

Assembly scanning is replaced by explicit registration, which a source generator can write for you:

```csharp
// Hand-written
service.AddMessages(typeof(PlaceOrder), typeof(OrderPlaced), typeof(GetQuote), typeof(Quote));

// Or generated: every type implementing IOrderMessage in this assembly
[ObvsMessages(typeof(IOrderMessage))]
public partial class OrderMessages;
```

- The generator emits a type list (and, later, serializer metadata), so there's no reflection scan at runtime. That makes Obvs trim-safe and AOT-friendly, and fixes the class of single-file problems in #42.
- Registration is per service, so it's clear which service owns which messages.

### 3. Analyzers with code fixes

Tracking: #86

The analyzer ships inside the main package, so it's on by default. First rules:

| Rule | Detects | Code fix |
|---|---|---|
| OBVS001 | A message type registered for a service doesn't implement that service's marker interface | Add the interface |
| OBVS002 | A type implements more than one of `ICommand`, `IEvent`, `IRequest` and `IResponse` | — |
| OBVS003 | A request handler's response type doesn't implement `IResponse` for the same service | — |
| OBVS004 | A service has no transport or no serializer configured | Add `Use<Transport>()` |
| OBVS005 | The result of `Subscribe` on a bus stream is discarded | Store the subscription, or suggest a handler |
| OBVS006 | An `async` lambda passed to `Subscribe` on a bus stream (compiles to `async void`) | Convert to `HandleCommand`, `HandleEvent` or `HandleRequest` |

Each diagnostic says what's wrong, how to fix it, and links to a docs page.

### 4. Handlers and awaitable requests

Tracking: #87

Obvs is the *observable* bus, and v7 keeps it that way: **observable-first, not observable-only**. Every service's messages stay `IObservable<T>` streams, and the transports still produce observables. What v7 adds are familiar entry points layered on that core, so the most common job ("when a command arrives, do some async work") is hard to get wrong.

#### Why the observable core alone isn't enough

Treating messages as streams is still the right idea. Rx makes things that are painful elsewhere simple: correlating messages across services, windowing, throttling, several subscribers to one stream, and requests that return many responses. But the most common use is reacting to one message with async work, and that is easy to get subtly wrong with Rx. The repo's own example service does it:

```csharp
// Examples/Service.ActiveMQ/Program.cs (v6)
_subscription = serviceBus.Commands.OfType<Command1>().Subscribe(async cmd =>
{
    await serviceBus.PublishAsync(new Event1 { Data = cmd.Data });
});
```

That lambda compiles to `async void`, so:
- an exception in it can crash the process, or go unnoticed;
- nothing limits how many run at once;
- nothing waits for in-flight work at shutdown.

Other common pitfalls:
- **Subscription lifetime.** A dropped `IDisposable` keeps the subscription running forever; disposing too early stops messages silently.
- **Cancellation by disposal.** Rx cancels by disposing, while the rest of .NET passes a `CancellationToken`.
- **Awaiting an observable waits for it to complete.** So `await bus.GetResponses(req)` hangs without `Take(1)` and a timeout.
- **`OnError` ends a stream.** That's why v6 routes errors to the separate `Exceptions` stream.
- **No backpressure.** A slow handler behind `ObserveOn` builds an unbounded queue.

Agents reproduce common patterns well and rare ones badly, and there is far more handler and `async`/`await` code in the world than Rx code.

#### Three layers, one engine

```
             ┌──────────────────────────────────────────┐
  Handlers   │ HandleCommand<T>(async (cmd, ctx, ct) =>)│  react to a message
             ├──────────────────────────────────────────┤
  Await      │ RequestAsync<TReq, TResp>(..., ct)       │  ask, get one answer
             │ Events<T>(ct) → IAsyncEnumerable<T>      │  pull at your own pace
             ├──────────────────────────────────────────┤
  Rx (core)  │ IObservable<T> Commands / Events / ...   │  reason about streams
             └──────────────────────────────────────────┘
                 transports produce observables
```

```csharp
// Handlers, registered with DI
obvs.AddService<IOrderMessage>("orders", service => service
    .HandleCommand<PlaceOrder>(async (cmd, ctx, ct) =>
    {
        await ctx.PublishAsync(new OrderPlaced(cmd.OrderId), ct);
    })
    .HandleRequest<GetQuote, Quote>(async (req, ctx, ct) => new Quote(req.Sku, 9.99m)));

// Or as classes
public sealed class PlaceOrderHandler(IOrderRepository orders) : ICommandHandler<PlaceOrder>
{
    public async Task HandleAsync(PlaceOrder command, MessageContext context, CancellationToken ct) { ... }
}

// Awaitable request/response
Quote quote = await client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), TimeSpan.FromSeconds(5), ct);

// Pull-based consumption with backpressure
await foreach (var placed in client.Events<OrderPlaced>(ct)) { ... }
```

The upper layers are thin adapters over the observable core, so Rx still does the plumbing. Roughly:

```csharp
// A handler
Commands.OfType<T>()
    .Select(cmd => Observable.FromAsync(ct => RunHandlerAsync(cmd, ct))) // DI scope, trace span, try/catch, retry
    .Merge(options.MaxConcurrency);                                      // bounded concurrency
// subscribed when the host starts, disposed when it stops, waiting for in-flight work

// RequestAsync
GetResponses(request).OfType<TResp>().Take(1).Timeout(timeout).ToTask(ct);

// Events<T>(ct): the observable feeds a bounded Channel<T>, read out as IAsyncEnumerable<T>
```

The streams remain first class for what Rx is uniquely good at:

```csharp
// Orders with no payment within 5 minutes
var unpaid = bus.Events.OfType<OrderPlaced>()
    .SelectMany(order => bus.Events.OfType<PaymentReceived>()
        .Where(p => p.OrderId == order.Id)
        .Take(1)
        .Timeout(TimeSpan.FromMinutes(5), Observable.Return<PaymentReceived>(null))
        .Where(p => p is null)
        .Select(_ => order));
```

#### Rules

- **Guidance:** the docs give one rule: *use a handler to react to a message, and use observables to reason about streams of messages.*
- **Cancellation:** every async method takes a `CancellationToken`.
- **Replies:** `MessageContext` carries the message's headers and correlation ID, and lets a handler publish, send or reply, so replies can't go to the wrong place.
- **Errors:** handler exceptions are logged, traced, and either retried or dead-lettered according to transport options. They don't vanish, and they never end the underlying stream.
- **One obvious way:** two ways to consume messages is a cost. Analyzer OBVS006 (see 3) flags `Subscribe(async ...)` on a bus stream and offers a code fix that turns it into a handler, so the guidance is enforced by the compiler, not a style guide.

#### The name

`IObservable` stays on `IServiceBus` in the core package (see open question 1). v7 also makes the bus more observable in the operational sense: OpenTelemetry traces (7), contracts the bus can describe itself (8), and an MCP `watch` tool that is a live view of the message streams (9).

### 5. A testing kit

Tracking: #88

A new `Obvs.Testing` package:

```csharp
await using var harness = await ObvsTestHarness.StartAsync(services => services
    .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", s => s
        .UseInMemory()
        .AddMessagesFrom<OrderMessages>()
        .HandleCommand<PlaceOrder, PlaceOrderHandler>())));

await harness.Client.SendAsync(new PlaceOrder("o-1"));

var placed = await harness.Published<OrderPlaced>(e => e.OrderId == "o-1");
```

- The in-memory transport has the same delivery semantics as the real transports: per-service routing, and request/response correlation.
- The `Published`, `Sent` and `Replied` assertions return as soon as a matching message arrives, and fail after a timeout.
- On timeout, the failure lists the messages that *were* seen, which is usually enough to diagnose the problem.
- Obvs's own non-broker tests move to it, replacing `Task.Delay`.

### 6. Fail fast with actionable errors

Tracking: #89

- At startup the bus validates its whole configuration: every service has a transport and a serializer, every registered message has a serializer, there are no duplicate handlers, and options are valid. All problems are reported in one exception, not one at a time.
- Exception messages follow one template: what was expected, what was found, and how to fix it, naming the API to call. For example:

  > No deserializer is registered for message type 'OrderPlaced' on service 'orders'. Register it with `AddMessages(typeof(OrderPlaced))`, or add `[ObvsMessages(typeof(IOrderMessage))]` to a partial class in its assembly, and check that 'OrderPlaced' implements 'IOrderMessage'.

- Configuration errors are never routed only to the `Exceptions` stream. Runtime delivery errors are logged and traced as well as being published there.
- Every error in the catalogue has a test asserting that its message names the fix.

### 7. OpenTelemetry and standard logging

Tracking: #90

- The core gets an `ActivitySource` and a `Meter` named `Obvs`. Send, publish, receive and handle become spans, following the OpenTelemetry messaging conventions, and trace context is carried in message headers across all transports.
- Metrics cover messages sent, received, handled and failed, and handler duration, tagged by service and message type.
- All logging goes through `Microsoft.Extensions.Logging`, with structured fields for service, message type and correlation ID.
- These replace `Obvs.Monitoring.*` and `Obvs.Logging.NLog` (see 10).

### 8. Self-describing service contracts

Tracking: #91

- Each service's contract is generated as an [AsyncAPI](https://www.asyncapi.com/) document: its channels and the commands, events, requests and responses on them, with a JSON Schema for each message.
- **At build time:** the source generator or an MSBuild task writes the document, so it can be committed and diffed. A CI check can then flag breaking changes, such as a removed field or a changed type.
- **At runtime:** `bus.DescribeAsync()` returns the same document. It feeds the MCP server (9).

### 9. An MCP server for development

Tracking: #92

`Obvs.Mcp` hosts a [Model Context Protocol](https://modelcontextprotocol.io/) server next to a development bus, so an agent can check its work against the real system:

| Tool | Purpose |
|---|---|
| `list_services` | Services on the bus, with their transports |
| `describe_service` | A service's contract (from 8) |
| `send_command` / `publish_event` | Send a message, validated against its schema |
| `request` | Send a request and return the response |
| `watch` | Stream live messages, with filters by service, type or field |
| `recent_errors` | Recent delivery and handler failures, with traces |

Safety:
- Off by default.
- Refuses to start outside the `Development` environment unless explicitly allowed.
- Never enabled by default in production builds.
- Sending can be disabled separately from inspecting.

### 10. Fewer packages, one obvious default

Tracking: #93

| v7 | Status |
|---|---|
| `Obvs` (core, including System.Text.Json serialization, the analyzers and the generator) | Core |
| `Obvs.RabbitMQ`, `Obvs.Kafka`, `Obvs.NATS`, `Obvs.ActiveMQ`, `Obvs.NetMQ` | Transports, all configured the same way |
| `Obvs.Serialization.MessagePack` (MessagePack-CSharp), `Obvs.Serialization.ProtoBuf` | Optional serializers |
| `Obvs.Testing`, `Obvs.Mcp` | New |
| `Obvs.Serialization.Json` (Newtonsoft) | Kept for v6 wire compatibility; not the default |
| MsgPack.Cli, NetJSON, XML, `Obvs.Monitoring.*`, `Obvs.Logging.NLog` | Stay on v6, deprecated on NuGet with named replacements |

Every transport is configured the same way: an options class, `Use<Transport>(IConfiguration)` and `Use<Transport>(Action<Options>)`.

### 11. Documentation agents can use

Tracking: #94

- `llms.txt` at the repo root and on the docs site: a concise map of the API and links to the recipes.
- `AGENTS.md`: how to build and test the repo, its conventions, and where the code lives.
- XML documentation on every public member, shipped in the packages. Missing docs fail the build (`CS1591` as an error for public APIs).
- A "common tasks" page with short recipes: add a service, send a command, handle a request, publish an event, test it, trace it.
- Every snippet in the README and docs comes from a samples project that CI builds and tests, so examples can't drift.

### 12. Migration path from v6

Tracking: #95

- Where possible, v7 APIs land alongside v6 in 6.x minor releases, so users can migrate gradually. The v6 APIs are marked `[Obsolete]` with messages naming the replacement, and removed in 7.0.
- A migration guide gives before-and-after snippets for each change. An optional analyzer code fix rewrites the common v6 fluent chains into `AddObvs` calls.
- **Wire compatibility:** v6 and v7 services on the same broker must interoperate, with the same exchange, topic and queue naming and the same default serialization for existing services. New defaults (System.Text.Json) apply only to services that opt in, or to new services. A test runs a v6 service and a v7 service on one broker.

## Phasing

| Phase | Ships in | Changes |
|---|---|---|
| A: additive foundations | 6.x minors | 1 (DI hosting), 5 (testing kit), 6 (validation and errors), 7 (OpenTelemetry), 11 (docs) |
| B: new programming model | 6.x minors, previews | 2 (registration and generator), 3 (analyzers), 4 (handlers and `RequestAsync`), 8 (contracts) |
| C: v7.0 | 7.0 | 10 (package consolidation), 12 (remove obsolete APIs), 9 (MCP server, once contracts exist) |

Phase A is useful on its own, and doesn't break anyone.

## Measuring success

"AI native" should be measured, not asserted. Build a small evaluation suite of agent tasks against a sample app:

- Add a new service with a command and an event.
- Add a request/response pair and a test for it.
- Find and fix a misconfigured service.
- Trace a message across two services.

Run them with a coding agent against v6 and against each v7 phase. Track:
- whether the task succeeds;
- how many compile and test iterations it takes;
- whether the tests the agent writes are flaky.

The same tasks double as onboarding material for people.

## Open questions

1. ~~**Rx's place.** Should the `IObservable` streams stay on `IServiceBus`, or move to an `Obvs.Reactive` package in 7.0?~~ **Resolved: they stay in the core.** Observables are the library's identity and the engine the handler and `await` layers are built on, so moving them out would cost more than it saves. See 4.
2. **Default serializer for new services.** System.Text.Json is proposed. Is that acceptable for users who mix v6 and v7 services by default?
3. **Retries and dead-lettering.** Should Obvs own these policies across transports, or delegate to each broker's features?
4. **Which transports to keep.** Is there enough use of ActiveMQ and NetMQ to justify porting them to the new model?
5. **The MCP server's scope.** Should it ever support read-only use against production, for incident debugging?
