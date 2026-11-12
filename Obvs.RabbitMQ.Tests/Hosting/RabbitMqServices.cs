using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Obvs.RabbitMQ.Tests.Hosting
{
    // Names services uniquely for this run, gives tests a channel to look at what the transport declared, and deletes the
    // run's queues and exchanges afterwards
    public sealed class RabbitMqServices : IAsyncDisposable
    {
        private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly ConcurrentQueue<string> _created = new ConcurrentQueue<string>();
        private readonly Lazy<Task<IConnection>> _connection = new Lazy<Task<IConnection>>(
            () => new ConnectionFactory { Uri = new Uri(TestBroker.Uri) }.CreateConnectionAsync());

        public string Create(string name)
        {
            var serviceName = $"obvs-rmq-{name}-{_runId}";
            _created.Enqueue(serviceName);
            return serviceName;
        }

        public async Task<IChannel> CreateChannelAsync()
        {
            return await (await _connection.Value).CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        // Null if the queue doesn't exist. Counts only messages waiting to be delivered, not ones delivered and not yet settled.
        public async Task<uint?> ReadyMessagesAsync(string queue)
        {
            await using var channel = await CreateChannelAsync();
            try
            {
                return (await channel.QueueDeclarePassiveAsync(queue, TestContext.Current.CancellationToken)).MessageCount;
            }
            catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == 404)
            {
                return null;
            }
        }

        // The next message on the queue, waiting for one to arrive
        public async Task<BasicGetResult> NextAsync(string queue, TimeSpan timeout)
        {
            await using var channel = await CreateChannelAsync();
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var result = await channel.BasicGetAsync(queue, true, TestContext.Current.CancellationToken);
                if (result != null)
                {
                    return result;
                }
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            throw new TimeoutException($"No message arrived on {queue} in time.");
        }

        public async ValueTask DisposeAsync()
        {
            if (!_connection.IsValueCreated && _created.IsEmpty)
            {
                return;
            }
            var connection = await _connection.Value;
            foreach (var serviceName in _created)
            {
                foreach (var queue in new[] { $"{serviceName}.Commands", $"{serviceName}.Requests", $"{serviceName}.DeadLetters" })
                {
                    await using var channel = await connection.CreateChannelAsync();
                    await channel.QueueDeleteAsync(queue);
                }
                foreach (var exchange in new[] { serviceName, $"{serviceName}.DeadLetters" })
                {
                    await using var channel = await connection.CreateChannelAsync();
                    await channel.ExchangeDeleteAsync(exchange);
                }
            }
            await connection.CloseAsync();
            connection.Dispose();
        }

        public static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null, string instanceId = null,
            Action<RabbitMqOptions> options = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs =>
            {
                if (instanceId != null)
                {
                    obvs.WithInstanceId(instanceId);
                }
                obvs.AddService<IOrderMessage>(serviceName, service =>
                {
                    service
                        .UseRabbitMq(rabbitMq =>
                        {
                            rabbitMq.Uri = new Uri(TestBroker.Uri);
                            options?.Invoke(rabbitMq);
                        })
                        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                    configure?.Invoke(service);
                });
            });
            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }

        public static IDictionary<string, object> QuorumQueueArguments(string serviceName, int maxDeliveryAttempts)
        {
            return new Dictionary<string, object>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = maxDeliveryAttempts,
                ["x-dead-letter-exchange"] = $"{serviceName}.DeadLetters",
            };
        }
    }
}
