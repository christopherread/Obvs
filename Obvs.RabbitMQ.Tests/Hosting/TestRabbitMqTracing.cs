using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // Two hosts connected only through RabbitMQ stand in for two processes
    public class TestRabbitMqTracing
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldContinueTheSendersTraceAcrossRabbitMq()
        {
            const string serviceName = "obvs-tracing-commands";
            using var activities = new ActivityRecorder();
            using var receiver = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            var send = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"send {serviceName}"));
            var process = await WaitForAsync(() => activities.For(serviceName).SingleOrDefault(activity => activity.DisplayName == $"process {serviceName}"));
            Assert.Equal(send.TraceId, process.TraceId);
            Assert.Equal(send.SpanId, process.ParentSpanId);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCarryHeadersTheAppSetsOverRabbitMq()
        {
            const string serviceName = "obvs-tracing-headers";
            var received = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var receiver = await StartHostAsync(serviceName, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    received.TrySetResult(ObvsMessageHeaders.Get(command));
                    return Task.CompletedTask;
                }));
            using var sender = await StartHostAsync(serviceName, service => service.AsClient());
            var placeOrder = new PlaceOrder { OrderId = "o-1" };
            ObvsMessageHeaders.Set(placeOrder, new Dictionary<string, string> { ["x-tenant"] = "acme" });

            await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(placeOrder, TestContext.Current.CancellationToken);

            var headers = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Equal("acme", headers["x-tenant"]);
        }

        private static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IOrderMessage>(serviceName, service => configure(service
                .UseRabbitMq(options => options.Uri = new Uri(TestBroker.Uri))
                .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>))))));
            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            return found ?? throw new TimeoutException($"The expected {typeof(T).Name} wasn't recorded in time.");
        }

        private sealed class ActivityRecorder : IDisposable
        {
            private readonly ConcurrentQueue<Activity> _stopped = new ConcurrentQueue<Activity>();
            private readonly ActivityListener _listener;

            public ActivityRecorder()
            {
                _listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == ObvsTelemetry.ActivitySourceName,
                    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = activity => _stopped.Enqueue(activity),
                };
                ActivitySource.AddActivityListener(_listener);
            }

            public IReadOnlyList<Activity> For(string serviceName)
            {
                return _stopped.Where(activity => (activity.GetTagItem("messaging.destination.name") as string) == serviceName).ToList();
            }

            public void Dispose() => _listener.Dispose();
        }
    }
}
