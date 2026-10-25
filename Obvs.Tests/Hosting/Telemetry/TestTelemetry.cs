using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Tests.Hosting.Handlers;
using Xunit;

namespace Obvs.Tests.Hosting.Telemetry
{
    public class TestTelemetry
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        // Unique per test, so a test sees only its own spans, measurements and logs
        private readonly string _service = "orders-" + Guid.NewGuid().ToString("N");

        [Fact]
        public async Task ShouldTraceSendingACommand()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await StartAsync(service => { });

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var span = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"send {_service}"));
            Assert.Equal(ActivityKind.Producer, span.Kind);
            Assert.Equal("obvs", span.GetTagItem("messaging.system"));
            Assert.Equal("send", span.GetTagItem("messaging.operation.name"));
            Assert.Equal(nameof(PlaceOrder), span.GetTagItem("obvs.message.type"));
        }

        [Fact]
        public async Task ShouldTracePublishingAnEvent()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await StartAsync(service => { });

            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));

            var span = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"publish {_service}"));
            Assert.Equal(ActivityKind.Producer, span.Kind);
            Assert.Equal("publish", span.GetTagItem("messaging.operation.name"));
            Assert.Equal(nameof(OrderPlaced), span.GetTagItem("obvs.message.type"));
        }

        [Fact]
        public async Task ShouldTraceRequestsAndReplies()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await StartAsync(service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))));

            await harness.Client.RequestAsync<GetQuote, Quote>(new GetQuote("sku-1"), Timeout, Ct);

            var request = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"request {_service}"));
            var reply = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"reply {_service}"));
            Assert.Equal(nameof(GetQuote), request.GetTagItem("obvs.message.type"));
            Assert.Equal(nameof(Quote), reply.GetTagItem("obvs.message.type"));
        }

        [Fact]
        public async Task ShouldTraceHandlingAMessage()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await StartAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var span = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"process {_service}"));
            Assert.Equal(ActivityKind.Consumer, span.Kind);
            Assert.Equal("process", span.GetTagItem("messaging.operation.type"));
            Assert.Equal(nameof(PlaceOrder), span.GetTagItem("obvs.message.type"));
            Assert.NotEqual(ActivityStatusCode.Error, span.Status);
        }

        [Fact]
        public async Task ShouldMarkTheHandlingSpanAsFailedWhenAHandlerThrows()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await StartAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => throw new InvalidOperationException("boom")));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var span = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"process {_service}"));
            Assert.Equal(ActivityStatusCode.Error, span.Status);
            Assert.Equal(typeof(InvalidOperationException).FullName, span.GetTagItem("error.type"));
            Assert.Contains(span.Events, activityEvent => activityEvent.Name == "exception");
        }

        [Fact]
        public async Task ShouldMakeMessagesSentByAHandlerChildrenOfItsSpan()
        {
            using var activities = new ActivityRecorder();
            await using var harness = await StartAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => context.PublishAsync(new OrderPlaced(command.OrderId), ct)));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var process = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"process {_service}"));
            var publish = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"publish {_service}"));
            Assert.Equal(process.TraceId, publish.TraceId);
            Assert.Equal(process.SpanId, publish.ParentSpanId);
        }

        [Fact]
        public async Task ShouldCountSentMessages()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => { });

            await harness.Client.SendAsync(new PlaceOrder("o-1"));
            await harness.Client.SendAsync(new PlaceOrder("o-2"));
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"));

            var sent = await WaitForAsync(() =>
            {
                var measurements = metrics.For(_service, "messaging.client.sent.messages");
                return measurements.Count == 3 ? measurements : null;
            });
            Assert.Equal(2, sent.Where(m => (string)m.Tags["messaging.operation.name"] == "send").Sum(m => m.Value));
            Assert.Equal(1, sent.Where(m => (string)m.Tags["messaging.operation.name"] == "publish").Sum(m => m.Value));
            Assert.All(sent, m => Assert.Equal("obvs", m.Tags["messaging.system"]));
        }

        [Fact]
        public async Task ShouldMeasureHandlingDuration()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => command.OrderId == "o-2"
                    ? throw new InvalidOperationException("boom")
                    : Task.CompletedTask));

            await harness.Client.SendAsync(new PlaceOrder("o-1"));
            await harness.Client.SendAsync(new PlaceOrder("o-2"));

            var durations = await WaitForAsync(() =>
            {
                var measurements = metrics.For(_service, "messaging.process.duration");
                return measurements.Count == 2 ? measurements : null;
            });
            Assert.All(durations, m => Assert.True(m.Value >= 0));
            Assert.All(durations, m => Assert.Equal(nameof(PlaceOrder), m.Tags["obvs.message.type"]));
            Assert.Single(durations, m => !m.Tags.ContainsKey("error.type"));
            Assert.Single(durations, m => m.Tags.TryGetValue("error.type", out var errorType) && (string)errorType == typeof(InvalidOperationException).FullName);
        }

        [Fact]
        public async Task ShouldLogSentMessagesAtDebugLevelOnly()
        {
            var logs = new ListLoggerProvider();
            await using var harness = await ObvsTestHarness.StartAsync(services => services
                .AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Debug))
                .AddObvs(obvs => obvs.AddService<IOrderMessage>(_service, service => service.UseInMemory())),
                cancellationToken: Ct);

            await harness.Client.SendAsync(new PlaceOrder("o-1"));

            var entry = await WaitForAsync(() => logs.Entries.FirstOrDefault(e => e.Message.Contains(_service) && e.Message.Contains(nameof(PlaceOrder))));
            Assert.Equal(LogLevel.Debug, entry.Level);
            Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Information && e.Message.Contains(_service));
        }

        private Task<ObvsTestHarness> StartAsync(Action<ObvsServiceBuilder> configure)
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>(_service, service => configure(service.UseInMemory()))),
                cancellationToken: Ct);
        }

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, Ct);
            }
            return found ?? throw new TimeoutException($"The expected {typeof(T).Name} wasn't recorded in time.");
        }
    }
}
