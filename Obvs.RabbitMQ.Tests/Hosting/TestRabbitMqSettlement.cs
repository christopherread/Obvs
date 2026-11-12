using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;
using static Obvs.RabbitMQ.Tests.Hosting.RabbitMqServices;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // Commands and requests go to a quorum queue the service's buses share, and stay there until a handler settles them
    // (see TestSettlement in Obvs.Tests)
    public class TestRabbitMqSettlement : IClassFixture<RabbitMqServices>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
        private readonly RabbitMqServices _services;

        public TestRabbitMqSettlement(RabbitMqServices services)
        {
            _services = services;
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeclareQuorumQueuesForCommandsAndRequestsWithTheDeliveryLimit()
        {
            var serviceName = _services.Create("quorum");

            using var host = await StartHostAsync(serviceName, options: options => options.MaxDeliveryAttempts = 3);

            await using var channel = await _services.CreateChannelAsync();
            foreach (var queue in new[] { $"{serviceName}.Commands", $"{serviceName}.Requests" })
            {
                Assert.NotNull(await _services.ReadyMessagesAsync(queue));
                // Declaring a queue that exists succeeds only if it's declared with the same settings
                await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                    arguments: QuorumQueueArguments(serviceName, 3), cancellationToken: Ct);
            }
            Assert.NotNull(await _services.ReadyMessagesAsync($"{serviceName}.DeadLetters"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeliverACommandAgainWhenItsBusStopsBeforeItsHandlerFinishes()
        {
            // As when the process crashes: the host isn't stopped, so the handler never finishes
            var serviceName = _services.Create("crash");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var crashed = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>(async (command, context, ct) =>
                {
                    started.TrySetResult(true);
                    await Task.Delay(System.Threading.Timeout.Infinite, ct);
                }));
            await crashed.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);
            await started.Task.WaitAsync(Timeout, Ct);

            crashed.Dispose();

            var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.TrySetResult(command.OrderId);
                    return Task.CompletedTask;
                }));
            Assert.Equal("o-1", await handled.Task.WaitAsync(Timeout, Ct));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRemoveACommandOnceItsHandlerSucceeds()
        {
            var serviceName = _services.Create("settle-on-success");
            var handled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.TrySetResult(true);
                    return Task.CompletedTask;
                }));
            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);
            await handled.Task.WaitAsync(Timeout, Ct);
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);

            // Disposing the bus returns any message it hasn't settled to the queue
            host.Dispose();

            Assert.Equal(0u, await _services.ReadyMessagesAsync($"{serviceName}.Commands"));
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

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);

            await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref attempts) == 2));
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRedeliverACommandWhoseSubscribeAsyncFails()
        {
            var serviceName = _services.Create("redeliver-rx");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var subscription = bus.Commands.OfType<PlaceOrder>().SubscribeAsync((command, ct) => Interlocked.Increment(ref attempts) == 1
                ? throw new InvalidOperationException("Out of stock")
                : Task.CompletedTask);

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);

            await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref attempts) == 2));
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeadLetterACommandAfterMaxDeliveryAttempts()
        {
            // Obvs dead-letters the message itself on its last attempt, so it can record why
            var serviceName = _services.Create("dead-letter");
            var attempts = 0;
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("Out of stock\r\nfor sku-1");
                }), options: options => options.MaxDeliveryAttempts = 2);

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);

            var deadLetter = await _services.NextAsync($"{serviceName}.DeadLetters", Timeout);
            Assert.Equal($"{serviceName}.Commands.PlaceOrder", deadLetter.RoutingKey);
            Assert.Contains("o-1", Encoding.UTF8.GetString(deadLetter.Body.ToArray()));
            Assert.Equal(2, Volatile.Read(ref attempts));
            var headers = deadLetter.BasicProperties.Headers;
            Assert.Equal("HandlerFailed", Header(headers, "obvs-dead-letter-reason"));
            Assert.Equal("System.InvalidOperationException: Out of stock for sku-1", Header(headers, "obvs-dead-letter-description"));
            Assert.Equal("System.InvalidOperationException", Header(headers, "obvs-exception-type"));
            Assert.Equal("2", Header(headers, "obvs-delivery-count"));
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
            Assert.Equal(2, Volatile.Read(ref attempts));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldRemoveCommandsNoHandlerHandlesWhenTheyArrive()
        {
            var serviceName = _services.Create("no-handler");
            var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            var received = bus.Commands.OfType<PlaceOrder>().FirstAsync().ToTask();

            await bus.SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);
            Assert.Equal("o-1", (await received.WaitAsync(Timeout, Ct)).OrderId);
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
            host.Dispose();

            Assert.Equal(0u, await _services.ReadyMessagesAsync($"{serviceName}.Commands"));
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
            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-1" }, Ct);
            await started.Task.WaitAsync(Timeout, Ct);

            var stopping = host.StopAsync(Ct);
            finish.SetResult(true);
            await stopping.WaitAsync(Timeout, Ct);
            host.Dispose();

            Assert.Equal(0u, await _services.ReadyMessagesAsync($"{serviceName}.Commands"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldReportAQueueDeclaredWithOtherSettingsAndHowToFixIt()
        {
            // For example a classic queue someone declared with the name Obvs uses
            var serviceName = _services.Create("classic");
            await using (var channel = await _services.CreateChannelAsync())
            {
                await channel.QueueDeclareAsync($"{serviceName}.Commands", durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
            }
            var errors = new ConcurrentQueue<Exception>();
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var reported = bus.Exceptions.Subscribe(errors.Enqueue);

            using var subscription = bus.Commands.Subscribe(_ => { }, errors.Enqueue);
            // The bus wraps what the source reports
            var error = await WaitForAsync(() => errors.SelectMany(Chain).FirstOrDefault(exception => exception.Message.Contains($"'{serviceName}'")));

            Assert.Contains($"'{serviceName}.Commands'", error.Message);
            Assert.Contains("quorum", error.Message);
            Assert.Contains("Delete the queue", error.Message);
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldWaitLongerBeforeDeclaringAQueueAgainEachTimeItFails()
        {
            // The bus subscribes again as soon as a source reports an error, so the source paces the retries
            var serviceName = _services.Create("retry");
            await using (var channel = await _services.CreateChannelAsync())
            {
                await channel.QueueDeclareAsync($"{serviceName}.Commands", durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
            }
            var reported = new ConcurrentQueue<DateTime>();
            using var host = await StartHostAsync(serviceName);
            var bus = host.Services.GetRequiredService<IServiceBus>();
            using var errors = bus.Exceptions.Subscribe(_ => reported.Enqueue(DateTime.UtcNow));

            using var subscription = bus.Commands.Subscribe(_ => { });
            await WaitUntilAsync(() => Task.FromResult(reported.Count >= 3));

            var times = reported.ToArray();
            var waits = times.Skip(1).Zip(times, (time, previous) => time - previous).Take(2).ToArray();
            Assert.True(waits[0] >= TimeSpan.FromMilliseconds(400), $"Waited {waits[0]} before the second attempt");
            Assert.True(waits[1] > waits[0], $"Waited {waits[0]}, then {waits[1]}");
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldDeadLetterACommandItCantReadAfterMaxDeliveryAttemptsAndCarryOn()
        {
            // Delivered again first, as during a rolling deployment another of the service's buses may be able to read it
            var serviceName = _services.Create("unreadable");
            var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.TrySetResult(command.OrderId);
                    return Task.CompletedTask;
                }), options: options => options.MaxDeliveryAttempts = 2);
            await using (var channel = await _services.CreateChannelAsync())
            {
                await channel.BasicPublishAsync(serviceName, $"{serviceName}.Commands.PlaceOrder", false, new BasicProperties(), Encoding.UTF8.GetBytes("not json"), Ct);
            }

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-2" }, Ct);

            Assert.Equal("o-2", await handled.Task.WaitAsync(Timeout, Ct));
            var deadLetter = await _services.NextAsync($"{serviceName}.DeadLetters", Timeout);
            Assert.Equal("not json", Encoding.UTF8.GetString(deadLetter.Body.ToArray()));
            Assert.Equal("Undeserializable", Header(deadLetter.BasicProperties.Headers, "obvs-dead-letter-reason"));
            Assert.Equal("2", Header(deadLetter.BasicProperties.Headers, "obvs-delivery-count"));
        }

        [Fact, Trait("Category", "Explicit")]
        public async Task ShouldSkipACommandOfATypeItDoesntKnow()
        {
            // Such as one a newer version of the service sends
            var serviceName = _services.Create("unknown-type");
            var handled = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = await StartHostAsync(serviceName, service => service
                .HandleCommand<PlaceOrder>((command, context, ct) =>
                {
                    handled.TrySetResult(command.OrderId);
                    return Task.CompletedTask;
                }));
            await using (var channel = await _services.CreateChannelAsync())
            {
                await channel.BasicPublishAsync(serviceName, $"{serviceName}.Commands.CancelOrder", false, new BasicProperties(), Encoding.UTF8.GetBytes("{}"), Ct);
            }

            await host.Services.GetRequiredService<IServiceBus>().SendAsync(new PlaceOrder { OrderId = "o-2" }, Ct);

            Assert.Equal("o-2", await handled.Task.WaitAsync(Timeout, Ct));
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
            host.Dispose();
            Assert.Equal(0u, await _services.ReadyMessagesAsync($"{serviceName}.Commands"));
            Assert.Equal(0u, await _services.ReadyMessagesAsync($"{serviceName}.DeadLetters"));
        }

        private static IEnumerable<Exception> Chain(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                yield return current;
            }
        }

        private static string Header(IDictionary<string, object> headers, string name)
        {
            Assert.True(headers.TryGetValue(name, out var value), $"No {name} header");
            return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString();
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

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, Ct);
            }
            return found ?? throw new TimeoutException($"The expected {typeof(T).Name} wasn't recorded in time.");
        }
    }
}
