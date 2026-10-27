# Service contracts

Obvs describes your bus's services as an [AsyncAPI](https://www.asyncapi.com/) 3.0 document. That shows people and agents what each service accepts and sends without reading its assemblies, and lets CI catch a change that would break another app.

## What's in it

For each service, a channel for each kind of message it has:
- `orders.commands`, `orders.events`, `orders.requests` and `orders.responses`;
- with the service (`x-obvs-service`) and its transport (`x-obvs-transport`);
- and each message's JSON Schema, from System.Text.Json.

Operations say what this app does on each channel:
- a server receives commands and requests, and sends events and responses;
- a client does the opposite.

The messages are those the service registers with `AddMessages`, or else those Obvs finds by scanning for its message interface, as the bus does.

The schemas describe the messages' JSON shape. A service that serializes with MessagePack or ProtoBuf sends the same fields, in another format.

## At run time

```csharp
var contracts = app.Services.GetRequiredService<ObvsContracts>();
string all = contracts.Describe();
string orders = contracts.Describe("orders");
```

A keyed bus has its own: `GetRequiredKeyedService<ObvsContracts>("analytics")`. Obvs.Mcp serves the same document to agents.

## Catching breaking changes in CI

Commit each bus's contract, and check it in a test with `ObvsContractSnapshot` from `Obvs.Testing`:

```csharp
[Fact]
public async Task TheOrdersContractHasNoBreakingChanges()
{
    await using var harness = await ObvsTestHarness.StartAsync(services => services.AddOrdersService());

    ObvsContractSnapshot.Verify(harness.Services, "contracts/orders.asyncapi.json");
}
```

- **The first run** writes the file, to commit.
- **The same contract:** the test passes.
- **A contract that only grows**, with new messages or new optional properties: the test updates the file and passes. Commit the update, so the history shows when the contract grew.
- **A breaking change:** the test fails, listing each change, and leaves the file as it was. Breaking changes are:
  - a message removed from a channel;
  - a property removed;
  - a property's type changed;
  - a property newly required.

  If a breaking change is intended, update every app that uses the messages, then delete the file and run again to accept it.

`ObvsContractComparer.FindBreakingChanges(before, after)` compares two documents, for checks of your own, such as comparing against the document from your last release.
