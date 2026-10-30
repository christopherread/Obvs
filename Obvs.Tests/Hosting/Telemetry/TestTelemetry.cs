using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Obvs.Configuration;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Tests.Hosting.Handlers;
using Obvs.Transports;
using Obvs.Types;
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

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

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

            await harness.Bus.PublishAsync(new OrderPlaced("o-1"), TestContext.Current.CancellationToken);

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

            await harness.Client.RequestAsync(new GetQuote("sku-1"), Timeout, Ct);

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

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

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

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

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

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

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

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);
            await harness.Client.SendAsync(new PlaceOrder("o-2"), TestContext.Current.CancellationToken);
            await harness.Bus.PublishAsync(new OrderPlaced("o-1"), TestContext.Current.CancellationToken);

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

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);
            await harness.Client.SendAsync(new PlaceOrder("o-2"), TestContext.Current.CancellationToken);

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
        public async Task ShouldCountEachReceivedMessageOnceHoweverManySubscribe()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            var seen = 0;
            using var subscription = harness.Bus.Commands.Subscribe(_ => Interlocked.Increment(ref seen));

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);
            await harness.Client.SendAsync(new PlaceOrder("o-2"), TestContext.Current.CancellationToken);

            await WaitForAsync(() => metrics.For(_service, "messaging.process.duration").Count == 2 && Volatile.Read(ref seen) == 2 ? new object() : null);
            var received = metrics.For(_service, "messaging.client.consumed.messages");
            Assert.Equal(2, received.Count);
            Assert.Equal(2, received.Sum(m => m.Value));
            Assert.All(received, m => Assert.Equal("obvs", m.Tags["messaging.system"]));
            Assert.All(received, m => Assert.Equal("receive", m.Tags["messaging.operation.name"]));
            Assert.All(received, m => Assert.Equal(nameof(PlaceOrder), m.Tags["obvs.message.type"]));
        }

        [Fact]
        public async Task ShouldCountReceivedMessagesNoHandlerTakes()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => { });
            var seen = 0;
            using var subscription = harness.Client.Events.Subscribe(_ => Interlocked.Increment(ref seen));

            await harness.Bus.PublishAsync(new OrderPlaced("o-1"), TestContext.Current.CancellationToken);

            // Counted as it arrives, just before it's delivered, so wait for both
            var received = await WaitForAsync(() =>
            {
                var measurements = metrics.For(_service, "messaging.client.consumed.messages");
                return measurements.Count == 1 && Volatile.Read(ref seen) == 1 ? measurements : null;
            });
            Assert.Equal(nameof(OrderPlaced), received.Single().Tags["obvs.message.type"]);
        }

        [Fact]
        public async Task ShouldCountReceivedRequestsAndTheirResponses()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => service
                .HandleRequest<GetQuote, Quote>((request, context, ct) => Task.FromResult(new Quote(request.Sku, 9.99m))));

            await harness.Client.RequestAsync(new GetQuote("sku-1"), Timeout, Ct);

            var received = await WaitForAsync(() =>
            {
                var measurements = metrics.For(_service, "messaging.client.consumed.messages");
                return measurements.Count == 2 ? measurements : null;
            });
            Assert.Equal(new[] { nameof(GetQuote), nameof(Quote) }, received.Select(m => (string)m.Tags["obvs.message.type"]).OrderBy(name => name));
        }

        [Fact]
        public async Task ShouldCountAResponseOnceWhenSeveralRequestsAreWaiting()
        {
            // Each request in flight sees every response on the service, but counts only its own
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => service
                .HandleRequest<GetQuote, Quote>(async (request, context, ct) =>
                {
                    await Task.Delay(50, ct);
                    return new Quote(request.Sku, 9.99m);
                }));

            await Task.WhenAll(Enumerable.Range(1, 3).Select(i => harness.Client.RequestAsync(new GetQuote($"sku-{i}"), Timeout, Ct)));

            await WaitForAsync(() => metrics.For(_service, "messaging.process.duration").Count == 3 ? new object() : null);
            await Task.Delay(100, Ct);
            Assert.Equal(3, metrics.For(_service, "messaging.client.consumed.messages").Count(m => (string)m.Tags["obvs.message.type"] == nameof(Quote)));
        }

        [Fact]
        public async Task ShouldCountFailedSendsWithTheErrorType()
        {
            using var metrics = new MetricRecorder();
            var endpointClient = A.Fake<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => endpointClient.CanHandle(A<IMessage>._)).Returns(true);
            A.CallTo(() => endpointClient.Events).Returns(System.Reactive.Linq.Observable.Never<IEvent>());
            A.CallTo(() => endpointClient.SendAsync(A<ICommand>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._)).ThrowsAsync(new TimeoutException("The broker didn't answer"));
            var provider = A.Fake<IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => provider.CreateEndpointClient()).Returns(endpointClient);
            await using var harness = await StartAsync(service => service.AsClient().UseEndpointProvider(_ => provider));

            await Assert.ThrowsAnyAsync<Exception>(() => harness.Client.SendAsync(new PlaceOrder("o-1"), Ct));

            var sent = Assert.Single(metrics.For(_service, "messaging.client.sent.messages"));
            Assert.Equal(typeof(TimeoutException).FullName, sent.Tags["error.type"]);
            Assert.Equal("send", sent.Tags["messaging.operation.name"]);
        }

        [Fact]
        public async Task ShouldMarkARequestThatFailedToSendAsFailed()
        {
            using var activities = new ActivityRecorder();
            using var metrics = new MetricRecorder();
            var transport = new RequestTransport { SendFailure = new TimeoutException("The broker didn't answer") };
            await using var harness = await StartAsync(service => service.AsClient().UseEndpointProvider(_ => transport));

            await Assert.ThrowsAsync<TimeoutException>(() => harness.Client.RequestAsync(new GetQuote("sku-1"), TimeSpan.FromMinutes(5), Ct))
                .WaitAsync(Timeout, Ct);

            var span = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"request {_service}"));
            Assert.Equal(ActivityStatusCode.Error, span.Status);
            Assert.Equal(typeof(TimeoutException).FullName, span.GetTagItem("error.type"));
            var sent = Assert.Single(metrics.For(_service, "messaging.client.sent.messages"));
            Assert.Equal(typeof(TimeoutException).FullName, sent.Tags["error.type"]);
            Assert.Equal("request", sent.Tags["messaging.operation.name"]);
        }

        [Fact]
        public async Task ShouldCountARequestAsSentOnceItsSendSucceeds()
        {
            using var activities = new ActivityRecorder();
            using var metrics = new MetricRecorder();
            var transport = new RequestTransport();
            await using var harness = await StartAsync(service => service.AsClient().UseEndpointProvider(_ => transport));

            await harness.Client.RequestAsync(new GetQuote("sku-1"), Timeout, Ct);

            var span = await WaitForAsync(() => activities.For(_service).SingleOrDefault(activity => activity.DisplayName == $"request {_service}"));
            Assert.NotEqual(ActivityStatusCode.Error, span.Status);
            var sent = await WaitForAsync(() => metrics.For(_service, "messaging.client.sent.messages").SingleOrDefault());
            Assert.False(sent.Tags.ContainsKey("error.type"));
        }

        [Fact]
        public async Task ShouldCountSuccessfulSendsWithoutAnErrorType()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => { });

            await harness.Client.SendAsync(new PlaceOrder("o-1"), Ct);

            var sent = await WaitForAsync(() => metrics.For(_service, "messaging.client.sent.messages").SingleOrDefault());
            Assert.False(sent.Tags.ContainsKey("error.type"));
        }

        [Fact]
        public void ShouldCountMessagesATransportCantDeserialize()
        {
            using var metrics = new MetricRecorder();

            ObvsTransportTelemetry.RecordUndeserializable(_service, nameof(PlaceOrder), new FormatException("Not JSON"));

            var received = Assert.Single(metrics.For(_service, "messaging.client.consumed.messages"));
            Assert.Equal(typeof(FormatException).FullName, received.Tags["error.type"]);
            Assert.Equal(nameof(PlaceOrder), received.Tags["obvs.message.type"]);
            Assert.Equal("receive", received.Tags["messaging.operation.name"]);
        }

        [Fact]
        public async Task ShouldCountMessagesWhoseHandlerFailed()
        {
            using var metrics = new MetricRecorder();
            await using var harness = await StartAsync(service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => command.OrderId == "o-2"
                    ? throw new InvalidOperationException("boom")
                    : Task.CompletedTask));

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);
            await harness.Client.SendAsync(new PlaceOrder("o-2"), TestContext.Current.CancellationToken);

            await WaitForAsync(() => metrics.For(_service, "messaging.process.duration").Count == 2 ? new object() : null);
            var failed = Assert.Single(metrics.For(_service, "obvs.process.failed.messages"));
            Assert.Equal(1, failed.Value);
            Assert.Equal(typeof(InvalidOperationException).FullName, failed.Tags["error.type"]);
            Assert.Equal("obvs", failed.Tags["messaging.system"]);
            Assert.Equal("process", failed.Tags["messaging.operation.name"]);
            Assert.Equal(nameof(PlaceOrder), failed.Tags["obvs.message.type"]);
        }

        [Fact]
        public async Task ShouldLogSentMessagesAtDebugLevelOnly()
        {
            var logs = new ListLoggerProvider();
            await using var harness = await ObvsTestHarness.StartAsync(services => services
                .AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Debug))
                .AddObvs(obvs => obvs.AddService<IOrderMessage>(_service, service => service.UseInMemory())),
                cancellationToken: Ct);

            await harness.Client.SendAsync(new PlaceOrder("o-1"), TestContext.Current.CancellationToken);

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
