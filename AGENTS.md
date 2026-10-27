# Working on Obvs

Notes for agents and people changing this repo. For using Obvs, start with the [README](README.md) and [llms.txt](llms.txt).

## Build and test

```
dotnet build Obvs.sln -c Release
dotnet test --solution Obvs.sln -c Release --no-build -- --filter-not-trait "Category=Explicit"
```

The second command runs the unit tests, which need nothing else. The tests use xUnit v3 on Microsoft Testing Platform, so test arguments go after `--`:
- `-- --filter-class "Obvs.Tests.Hosting.TestContracts"` runs one class;
- `-- --filter-method "*ShouldDescribeOneService"` runs one test.

Tests that need a broker carry `[Trait("Category", "Explicit")]`. Run them per transport, against local brokers started as CI starts them (see `.github/workflows/dotnet.yml`):

```
docker run -d -p 5672:5672 rabbitmq:4.4-alpine
docker run -d -p 4222:4222 nats:2.15
docker run -d -p 4223:4222 nats:2.15 -js
docker run -d -p 9092:9092 apache/kafka:4.4.0
docker run -d -p 61616:61616 apache/activemq-classic:6.2.0
docker compose -f Obvs.Kafka.Tests/SaslBroker/docker-compose.yml up -d
docker compose -f Obvs.AzureSB.Tests/Emulator/docker-compose.yml up -d
docker compose -f Obvs.AmazonSqs.Tests/LocalStack/docker-compose.yml up -d

dotnet test --project Obvs.RabbitMQ.Tests -c Release --no-build -- --filter-trait "Category=Explicit"
```

The build has no warnings. Keep it that way.

## Where things are

| Path | What |
|---|---|
| `Obvs/` | The core: `IServiceBus`, endpoints, serialization, and `Hosting/`, where `AddObvs`, handlers, telemetry, contracts and `ObvsBus` live |
| `Obvs.<Transport>/` | A transport. Its `<Transport>TransportExtensions.cs` has `Use<Transport>(...)`, its options and its startup checks |
| `Obvs.Serialization.*/` | Serializers besides the built-in System.Text.Json |
| `Obvs.Generators/`, `Obvs.Analyzers/`, `Obvs.Analyzers.CodeFixes/` | The `[ObvsMessages]` source generator, and the analyzers and their code fixes, all shipped in the `Obvs` package |
| `Obvs.Testing/` | The in-memory transport and `ObvsTestHarness` |
| `Obvs.Mcp/` | The MCP server for development buses |
| `Obvs.Samples/` | Every C# snippet in the README and `docs/`, compiled, with the recipes run as tests |
| `*.Tests/` | Tests, one project per package |
| `docs/` | Guides; `v7-design.md` is the design for v7 |
| `Examples/` | A client and a service on ActiveMQ, with their own solution |

## Conventions

- **Test first.** Write the failing test, see it fail for the right reason, then make it pass. Test names say the behaviour: `ShouldDescribeOneService`.
- **Language versions.** The core and the transports also target `net472` and `netstandard2.0`, which compile as C# 7.3: no nullable reference types, no async streams or `await using`, no default interface members. Tests, `Obvs.Mcp` and the Roslyn projects target newer frameworks.
- **Packages** are versioned centrally in `Directory.Packages.props`.
- **Errors say how to fix them.** A startup check names the service, what's wrong and the API that fixes it, and has a test asserting its message names the fix (see `docs/configuration-errors.md`).
- **Snippets.** Code in the README or a doc page comes from a region in `Obvs.Samples`:
  - mark the region `// snippet: name` ... `// end-snippet`;
  - put `<!-- snippet: name -->` before the doc's C# block;
  - run `OBVS_UPDATE_SNIPPETS=1 dotnet test --project Obvs.Samples` to copy the regions in.

  `TestSnippets` fails when a doc and its sample differ. A block that can't compile gets `<!-- not compiled: why -->`.
- **XML docs** on every public member, which ship in the packages. A missing one fails the build (CS1591 is an error). The exceptions are code kept for v6 apps, such as the fluent configuration, v6's logging and monitoring abstractions, and the v6 transports' building blocks, and code copied into the repo. Each of those files says so above its `#pragma warning disable`. Implementations of a documented interface use `/// <inheritdoc />`.
- **Wire compatibility.** v6 and v7 apps share brokers. Don't change routing names or JSON shapes without a test that a v6 app still understands them (see `Obvs.RabbitMQ.Tests/Hosting/TestV6Interop.cs`).

## Pull requests

Work goes into the `v7` branch, one pull request per change, with the v7 milestone. CI builds, runs every broker suite and the unit tests, and publishes packages from `master` and tags only.
