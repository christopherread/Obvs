using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Obvs.Hosting;
using Obvs.NATS.Tests.Hosting;
using Obvs.Serialization.Json;
using Xunit;

namespace Obvs.NATS.Tests.JetStream
{
    // Names services uniquely for this run, gives tests a JetStream context to look at what the transport created,
    // and deletes the run's streams afterwards
    public sealed class JetStreamServices : IAsyncDisposable
    {
        private readonly string _runId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly NatsConnection _connection = new NatsConnection(new NatsOpts { Url = TestBroker.JetStreamUrl });

        public JetStreamServices()
        {
            JetStream = new NatsJSContext(_connection);
        }

        public NatsJSContext JetStream { get; }

        public string Create(string name) => $"obvs-js-{name}-{_runId}";

        public async Task<bool> StreamExistsAsync(string stream)
        {
            try
            {
                await JetStream.GetStreamAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
                return true;
            }
            catch (NatsJSApiException exception) when (exception.Error.Code == 404)
            {
                return false;
            }
        }

        public async Task<ConsumerConfig> ConsumerConfigAsync(string stream, string consumer)
        {
            try
            {
                return (await JetStream.GetConsumerAsync(stream, consumer, TestContext.Current.CancellationToken)).Info.Config;
            }
            catch (NatsJSApiException exception) when (exception.Error.Code == 404)
            {
                return null;
            }
        }

        public async Task<long> MessagesOnStreamAsync(string stream)
        {
            var info = await JetStream.GetStreamAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            return info.Info.State.Messages;
        }

        public async ValueTask DisposeAsync()
        {
            var streams = new List<string>();
            await foreach (var stream in JetStream.ListStreamNamesAsync())
            {
                if (stream.Contains(_runId))
                {
                    streams.Add(stream);
                }
            }
            foreach (var stream in streams)
            {
                await JetStream.DeleteStreamAsync(stream);
            }
            await _connection.DisposeAsync();
        }

        public static async Task<IHost> StartHostAsync(string serviceName, Action<ObvsServiceBuilder> configure = null, string instanceId = null,
            Action<NatsJetStreamOptions> options = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs =>
            {
                if (instanceId != null)
                {
                    obvs.UseInstanceId(instanceId);
                }
                obvs.AddService<IOrderMessage>(serviceName, service =>
                {
                    service
                        .UseNatsJetStream(jetStream =>
                        {
                            jetStream.Url = TestBroker.JetStreamUrl;
                            options?.Invoke(jetStream);
                        })
                        .SerializedWith(new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)));
                    configure?.Invoke(service);
                });
            });
            var host = builder.Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }
    }
}
