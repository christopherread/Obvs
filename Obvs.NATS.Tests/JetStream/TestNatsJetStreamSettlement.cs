using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Obvs.NATS.Tests.Hosting;
using Xunit;
using static Obvs.NATS.Tests.JetStream.JetStreamServices;

namespace Obvs.NATS.Tests.JetStream
{
    // Messages a handler handles stay on their stream until the handler settles them (see TestSettlement in Obvs.Tests)
    public class TestNatsJetStreamSettlement : IClassFixture<JetStreamServices>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly JetStreamServices _services;

        public TestNatsJetStreamSettlement(JetStreamServices services)
        {
            _services = services;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepACommandOnItsStreamUntilItsHandlerSucceeds()
        {
            var serviceName = _services.Create("settle-on-success");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" });
            await started.Task.WaitAsync(Timeout, Ct);

            Assert.Equal(1, await _services.MessagesOnStreamAsync($"{serviceName}_Commands"));
            finish.SetResult(true);
            await WaitUntilAsync(async () => await _services.MessagesOnStreamAsync($"{serviceName}_Commands") == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseHandlerFails()
        {
            var serviceName = _services.Create("redeliver");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) => Interlocked.Increment(ref attempts) == 1
                    ? throw new InvalidOperationException("Out of stock")
                    : Task.CompletedTask));

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" });

            await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref attempts) == 2));
            await WaitUntilAsync(async () => await _services.MessagesOnStreamAsync($"{serviceName}_Commands") == 0);
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeadLetterACommandAfterMaxDeliveryAttempts()
        {
            // JetStream has no dead-letter destination, so Obvs republishes the message to the service's dead-letter stream
            var serviceName = _services.Create("dead-letter");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("Out of stock\r\nfor sku-1");
                }), options: options => options.MaxDeliveryAttempts = 2);
            var deadLetters = await _services.JetStream.CreateOrUpdateConsumerAsync($"{serviceName}_DeadLetters",
                new ConsumerConfig("test") { AckPolicy = ConsumerConfigAckPolicy.None }, Ct);

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" });

            var deadLetter = await deadLetters.NextAsync<string>(opts: new NatsJSNextOpts { Expires = Timeout }, cancellationToken: Ct);
            Assert.NotNull(deadLetter);
            var message = deadLetter;
            Assert.Equal($"{serviceName}.DeadLetters.Commands.PlaceOrder", message.Subject);
            Assert.Contains("o-1", message.Data);
            Assert.Equal(2, Volatile.Read(ref attempts));
            Assert.Equal("HandlerFailed", message.Headers["obvs-dead-letter-reason"].ToString());
            Assert.Equal("System.InvalidOperationException: Out of stock for sku-1", message.Headers["obvs-dead-letter-description"].ToString());
            Assert.Equal("System.InvalidOperationException", message.Headers["obvs-exception-type"].ToString());
            Assert.Equal("2", message.Headers["obvs-delivery-count"].ToString());
            await WaitUntilAsync(async () => await _services.MessagesOnStreamAsync($"{serviceName}_Commands") == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRemoveCommandsNoHandlerHandlesWhenTheyArrive()
        {
            var serviceName = _services.Create("no-handler");
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var received = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.Equal("o-1", (await received.WaitAsync(Timeout, Ct)).OrderId);
            await WaitUntilAsync(async () => await _services.MessagesOnStreamAsync($"{serviceName}_Commands") == 0);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldFinishAndCompleteARunningCommandWhenTheHostStops()
        {
            var serviceName = _services.Create("stop");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await finish.Task;
                }));
            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" });
            await started.Task.WaitAsync(Timeout, Ct);

            var stopping = host.StopAsync(Ct);
            finish.SetResult(true);
            await stopping.WaitAsync(Timeout, Ct);
            host.Dispose();

            Assert.Equal(0, await _services.MessagesOnStreamAsync($"{serviceName}_Commands"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldCreateConsumersWithTheDeliveryLimitAndAckWait()
        {
            var serviceName = _services.Create("delivery-limit");
            const string instanceId = "orders-worker-1";

            using var host = await StartHostAsync(serviceName, instanceId: instanceId, options: options =>
            {
                options.MaxDeliveryAttempts = 3;
                options.AckWait = TimeSpan.FromSeconds(20);
            });

            foreach (var (stream, consumer) in new[]
                     {
                         ($"{serviceName}_Commands", serviceName),
                         ($"{serviceName}_Requests", serviceName),
                         ($"{serviceName}_Events", instanceId),
                         ($"{serviceName}_Responses", instanceId),
                     })
            {
                var config = await _services.ConsumerConfigAsync(stream, consumer);
                Assert.Equal(3, config.MaxDeliver);
                Assert.Equal(TimeSpan.FromSeconds(20), config.AckWait);
                Assert.Equal(ConsumerConfigAckPolicy.Explicit, config.AckPolicy);
            }
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldKeepARunningCommandInProgressPastItsAckWait()
        {
            var serviceName = _services.Create("in-progress");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    await Task.Delay(TimeSpan.FromSeconds(4), ct);
                }, handler => handler.MaxConcurrency = 2), options: options =>
            {
                options.AckWait = TimeSpan.FromSeconds(1);
                options.MaxConcurrentMessages = 2;
            });

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" });

            await WaitUntilAsync(async () => await _services.MessagesOnStreamAsync($"{serviceName}_Commands") == 0);
            Assert.Equal(1, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldStopKeepingACommandInProgressAfterMaxInProgressDuration()
        {
            // Like a lock that's no longer renewed: JetStream delivers the command again while its first handler still runs
            var serviceName = _services.Create("in-progress-limit");
            var attempts = 0;
            var redelivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    if (Interlocked.Increment(ref attempts) == 2)
                    {
                        redelivered.TrySetResult(true);
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }, handler => handler.MaxConcurrency = 2), options: options =>
            {
                options.AckWait = TimeSpan.FromSeconds(1);
                options.MaxInProgressDuration = TimeSpan.Zero;
                options.MaxConcurrentMessages = 2;
            });

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" });

            Assert.True(await redelivered.Task.WaitAsync(Timeout, Ct));
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!await condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The condition wasn't met in time.");
                }
                await Task.Delay(200, Ct);
            }
        }
    }
}
