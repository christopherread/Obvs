using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs;
using Obvs.Example.Messages;
using Obvs.Hosting;
using OpenTelemetry.Trace;

// A client of the service: each line typed is sent as a command, "?" followed by a question makes a request, and the
// service's events are printed as they arrive. Sending starts a trace that the service continues.
var builder = Host.CreateApplicationBuilder(args);
var serviceName = Environment.GetEnvironmentVariable("OBVS_SERVICE_NAME") ?? "Obvs.Service1";

builder.Services.AddObvs(obvs => obvs
    .AddService<IServiceMessage1>(serviceName, service => service
        .UseActiveMq(options =>
        {
            options.BrokerUri = Environment.GetEnvironmentVariable("ACTIVEMQ_BROKER_URI") ?? "activemq:tcp://localhost:61616";
            options.UserName = "admin";
            options.Password = "admin";
        })
        .AsClient()
        .HandleEvent<Event1>((ev, context, ct) =>
        {
            Console.WriteLine($"Received event: {ev}");
            return Task.CompletedTask;
        })));

builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
    .AddSource(ObvsTelemetry.ActivitySourceName)
    .AddConsoleExporter());

using var host = builder.Build();
await host.StartAsync();
var client = host.Services.GetRequiredService<IServiceBusClient>();

Console.WriteLine("Type some text and hit <Enter> to send it as a command, or start with ? to ask a question. An empty line quits.");
string line;
while (!string.IsNullOrEmpty(line = Console.ReadLine()))
{
    if (line.StartsWith('?'))
    {
        var response = await client.RequestAsync(new Request1 { Question = line.Substring(1).Trim() }, TimeSpan.FromSeconds(10));
        Console.WriteLine($"Received response: {response.Answer}");
    }
    else
    {
        await client.SendAsync(new Command1 { Data = line });
    }
}

await host.StopAsync();
