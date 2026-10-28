# An MCP server for development

`Obvs.Mcp` serves a [Model Context Protocol](https://modelcontextprotocol.io/) server from your app while it runs, so an agent can check its work against the real bus:
- see the services and their contracts;
- send a command, publish an event or make a request;
- watch what happens;
- read the errors.

```
dotnet add package Obvs.Mcp
```

<!-- snippet: mcp-setup -->
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddObvs(obvs => obvs
    .AddService<IOrderMessage>("orders", service => service
        .UseRabbitMq(builder.Configuration.GetSection("Obvs:Orders:RabbitMq"))));
builder.Services.AddObvsMcp();

var app = builder.Build();
app.MapObvsMcp(); // at /obvs/mcp
app.Run();
```

Then point your agent at `http://localhost:<port>/obvs/mcp`, using MCP's Streamable HTTP transport. For Claude Code:

```
claude mcp add --transport http obvs http://localhost:5000/obvs/mcp
```

## Tools

| Tool | What it does |
|---|---|
| `list_services` | The bus's services: each one's transport, whether this app is its client or server, and its commands, events, requests and responses |
| `describe_service` | A service's contract: an AsyncAPI document with a JSON Schema for each message (see [contracts](contracts.md)) |
| `send_command` | Sends a command, given as JSON |
| `publish_event` | Publishes an event, given as JSON |
| `request` | Sends a request, given as JSON, and returns its response: the one the request declares, or else the first |
| `watch` | The bus's recent messages that match a service or message type, waiting for the first if none has arrived yet |
| `recent_errors` | Recent errors, newest first: failed handlers, and errors transports reported, with trace IDs |

- **Messages given as JSON:** a message sent as JSON is checked by deserializing it as its type. If it isn't one, the agent gets the error and where to find the type's schema.
- **What `watch` sees:** the commands and requests this app receives as a server, and the events it receives as a client. It keeps the last 200; set `RecentMessages` to keep more.

## Safety

- **Off unless added:** the server only exists if you call `AddObvsMcp` and `MapObvsMcp`.
- **Development only:** `MapObvsMcp` throws outside the Development environment. An agent that can send messages can do real work. To run it in a shared test environment, set `AllowOutsideDevelopment`, and consider turning sending off.
- **Inspection only:** with `AllowSending = false`, the server offers only `list_services`, `describe_service`, `watch` and `recent_errors`.

<!-- snippet: mcp-options -->
```csharp
builder.Services.AddObvsMcp(options =>
{
    options.AllowOutsideDevelopment = true;
    options.AllowSending = false;
});
```

Don't map it in production. It has no authentication of its own: put it behind your app's, with `MapObvsMcp().RequireAuthorization()`, if anyone else can reach the app.
