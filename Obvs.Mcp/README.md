# Obvs.Mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server for [Obvs](https://github.com/christopherread/Obvs) development buses: agents list services and their contracts, send messages, and watch what happens.

```bash
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

`MapObvsMcp` throws outside the Development environment unless you set `AllowOutsideDevelopment`. For its tools and options, see [MCP server](https://github.com/christopherread/Obvs/blob/master/docs/mcp.md).

[Obvs](https://github.com/christopherread/Obvs) is a .NET library for messaging between services: commands, events, requests and responses, handled by your code or read as Rx streams. Start with its [README](https://github.com/christopherread/Obvs#readme).
