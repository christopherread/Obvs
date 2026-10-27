using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Obvs.NATS.Tests.Hosting;
using Xunit;
using static Obvs.NATS.Tests.JetStream.JetStreamServices;

namespace Obvs.NATS.Tests.JetStream
{
    // A bus keeps receiving when its server goes away and comes back. Each test runs a nats-server of its own (see
    // RestartableJetStreamServer), which it kills and starts again with the same storage, so streams and consumers survive.
    // A bus reports receive errors on its Exceptions, and subscribes to the source again.
    public class TestNatsJetStreamReconnect
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        // The client tries to reconnect every few seconds, then the bus pulls again
        private static readonly TimeSpan Resumes = TimeSpan.FromSeconds(15);
        private const string ServiceName = "orders";

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepHandlingCommandsAfterTheServerRestarts()
        {
            await using var server = await RestartableJetStreamServer.StartAsync();
            var handled = new ConcurrentQueue<string>();
            using var worker = await StartHostAsync(server, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => { handled.Enqueue(command.OrderId); return Task.CompletedTask; }));
            var errors = RecordErrors(worker.Services.GetRequiredService<IServiceBus>());
            using (var sender = await StartHostAsync(server, service => service.AsClient()))
            {
                await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
                await WaitUntilAsync(() => handled.Contains("o-1"));
            }

            await server.RestartAsync();
            var restarted = Stopwatch.StartNew();
            using (var sender = await StartHostAsync(server, service => service.AsClient()))
            {
                await sender.Services.GetRequiredService<IServiceBusClient>().SendAsync(new PlaceOrder { OrderId = "o-2" }, TestContext.Current.CancellationToken);
                await WaitUntilAsync(() => handled.Contains("o-2"));
            }

            Assert.True(restarted.Elapsed < Resumes, $"Handled the command {restarted.Elapsed} after the restart.");

            Assert.Empty(errors);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepReceivingEventsAfterTheServerRestarts()
        {
            await using var server = await RestartableJetStreamServer.StartAsync();
            using var subscriber = await StartHostAsync(server, service => service.AsClient());
            var bus = subscriber.Services.GetRequiredService<IServiceBusClient>();
            var errors = RecordErrors(bus);
            var received = new ConcurrentQueue<string>();
            using var subscription = bus.Events.OfType<OrderPlaced>().Subscribe(placed => received.Enqueue(placed.OrderId));
            await Task.Delay(500, Ct);

            await server.RestartAsync();
            var restarted = Stopwatch.StartNew();
            using var publisher = await StartHostAsync(server, service => service.AsServer());
            await publisher.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => received.Contains("o-1"));
            Assert.True(restarted.Elapsed < Resumes, $"Received the event {restarted.Elapsed} after the restart.");
            Assert.Empty(errors);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCompleteACommandWhoseHandlerWasRunningWhileTheServerRestarted()
        {
            // Its acknowledgement goes once the bus has reconnected, so the command isn't handled again
            await using var server = await RestartableJetStreamServer.StartAsync();
            var attempts = 0;
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var worker = await StartHostAsync(server, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    started.TrySetResult(true);
                    await finish.Task;
                }));
            await worker.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, TestContext.Current.CancellationToken);
            await started.Task.WaitAsync(Timeout, Ct);

            await server.RestartAsync();
            finish.SetResult(true);

            await using var connection = new NatsConnection(new NatsOpts { Url = server.Url });
            var jetStream = new NatsJSContext(connection);
            await WaitUntilAsync(async () => (await jetStream.GetStreamAsync($"{ServiceName}_Commands", cancellationToken: Ct)).Info.State.Messages == 0);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(1, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldStopPromptlyWhileTheServerIsDown()
        {
            await using var server = await RestartableJetStreamServer.StartAsync();
            var host = await StartHostAsync(server, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var events = bus.Events.Subscribe(_ => { }, _ => { });
            await server.StopAsync();
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);

            var stopping = Stopwatch.StartNew();
            await host.StopAsync(Ct);
            host.Dispose();

            Assert.True(stopping.Elapsed < TimeSpan.FromSeconds(5), $"Stopping took {stopping.Elapsed}.");
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRecreateTheBusesOwnEventConsumerWhenItIsDeleted()
        {
            // It deletes itself after 5 minutes without a pull, as when the bus can't reach the server for that long
            await using var server = await RestartableJetStreamServer.StartAsync();
            const string instanceId = "orders-worker-1";
            using var subscriber = await StartHostAsync(server, service => service.AsClient(), instanceId);
            var bus = subscriber.Services.GetRequiredService<IServiceBusClient>();
            var errors = RecordErrors(bus);
            var received = new ConcurrentQueue<string>();
            using var subscription = bus.Events.OfType<OrderPlaced>().Subscribe(placed => received.Enqueue(placed.OrderId));
            await Task.Delay(500, Ct);
            await using var connection = new NatsConnection(new NatsOpts { Url = server.Url });
            var jetStream = new NatsJSContext(connection);

            await jetStream.DeleteConsumerAsync($"{ServiceName}_Events", instanceId, Ct);
            await WaitUntilAsync(async () => await ConsumerExistsAsync(jetStream, $"{ServiceName}_Events", instanceId));
            using var publisher = await StartHostAsync(server, service => service.AsServer());
            await publisher.Services.GetRequiredService<IServiceBus>().PublishAsync(new OrderPlaced { OrderId = "o-1" }, TestContext.Current.CancellationToken);

            await WaitUntilAsync(() => received.Contains("o-1"));
            Assert.Empty(errors);
            var config = (await jetStream.GetConsumerAsync($"{ServiceName}_Events", instanceId, Ct)).Info.Config;
            Assert.Equal(TimeSpan.FromMinutes(5), config.InactiveThreshold);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReportAnErrorToSubscribersWhenTheSharedCommandConsumerIsDeleted()
        {
            // Another bus of the service may be reading it, so an operator deleted it on purpose
            await using var server = await RestartableJetStreamServer.StartAsync();
            using var worker = await StartHostAsync(server, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            var errors = RecordErrors(worker.Services.GetRequiredService<IServiceBus>());
            await using var connection = new NatsConnection(new NatsOpts { Url = server.Url });

            await new NatsJSContext(connection).DeleteConsumerAsync($"{ServiceName}_Commands", ServiceName, Ct);

            var error = await WaitForErrorAsync(errors);
            Assert.Equal(10014, Assert.IsType<NatsJSApiException>(error.InnerException).Error.ErrCode);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReportAnErrorToSubscribersWhenTheCommandStreamIsDeleted()
        {
            await using var server = await RestartableJetStreamServer.StartAsync();
            using var worker = await StartHostAsync(server, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            var errors = RecordErrors(worker.Services.GetRequiredService<IServiceBus>());
            await using var connection = new NatsConnection(new NatsOpts { Url = server.Url });

            await new NatsJSContext(connection).DeleteStreamAsync($"{ServiceName}_Commands", Ct);

            var error = await WaitForErrorAsync(errors);
            Assert.Equal(10059, Assert.IsType<NatsJSApiException>(error.InnerException).Error.ErrCode);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReportAnErrorToSubscribersWhenTheServerComesBackWithoutJetStream()
        {
            await using var server = await RestartableJetStreamServer.StartAsync();
            using var worker = await StartHostAsync(server, service => service.AsServer()
                .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask));
            var errors = RecordErrors(worker.Services.GetRequiredService<IServiceBus>());

            await server.StopAsync();
            await server.StartAgainAsync(jetStream: false);

            var error = await WaitForErrorAsync(errors);
            Assert.Contains("JetStream", error.InnerException?.Message);
        }

        private static Task<IHost> StartHostAsync(RestartableJetStreamServer server, Action<Obvs.Hosting.ObvsServiceBuilder> configure,
            string instanceId = null)
        {
            return JetStreamServices.StartHostAsync(ServiceName, configure, instanceId, options => options.Url = server.Url);
        }

        private static ConcurrentQueue<Exception> RecordErrors(IServiceBusClient bus)
        {
            var errors = new ConcurrentQueue<Exception>();
            bus.Exceptions.Subscribe(errors.Enqueue, Ct);
            return errors;
        }

        private static async Task<bool> ConsumerExistsAsync(INatsJSContext jetStream, string stream, string consumer)
        {
            try
            {
                await jetStream.GetConsumerAsync(stream, consumer, Ct);
                return true;
            }
            catch (NatsJSApiException exception) when (exception.Error.Code == 404)
            {
                return false;
            }
        }

        private static async Task<Exception> WaitForErrorAsync(ConcurrentQueue<Exception> errors)
        {
            await WaitUntilAsync(() => !errors.IsEmpty);
            return errors.First();
        }

        private static Task WaitUntilAsync(Func<bool> condition) => WaitUntilAsync(() => Task.FromResult(condition()));

        private static async Task WaitUntilAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!await condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(100, Ct);
            }
        }
    }
}
