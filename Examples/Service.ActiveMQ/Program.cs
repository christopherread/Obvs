using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Example.Messages;
using Obvs.Hosting;
using OpenTelemetry.Trace;

// A service: it handles Command1 by publishing Event1, and answers Request1. Each message's trace continues from the
// client that sent it, so the console shows the same trace ID in both apps.
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
        .AsServer()
        .HandleCommand<Command1>(async (command, context, ct) =>
        {
            Console.WriteLine($"Received command: {command}");
            await context.PublishAsync(new Event1 { Data = command.Data.ToUpperInvariant() }, ct);
        })
        .HandleRequest<Request1, Response1>((request, context, ct) =>
            Task.FromResult(new Response1 { Answer = $"You asked: {request.Question}" }))));

builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
    .AddSource(ObvsTelemetry.ActivitySourceName)
    .AddConsoleExporter());

await builder.Build().RunAsync();
