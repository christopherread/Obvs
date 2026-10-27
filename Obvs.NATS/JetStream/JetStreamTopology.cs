using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Obvs.Hosting;

namespace Obvs.NATS.JetStream
{
    // A service's streams, the consumers its buses share, and this bus's own consumers. Each kind of message has its own
    // stream, and its subjects end in the message's type name: orders.Commands.PlaceOrder, orders.Responses.{instance ID}.Quote.
    internal sealed class JetStreamTopology
    {
        // A bus's own consumers delete themselves after this long without a pull, so a crashed bus doesn't leave them behind
        private static readonly TimeSpan BusConsumerInactiveThreshold = TimeSpan.FromMinutes(5);

        // JetStream's error codes for a stream or consumer that doesn't exist, and for a stream another bus created first
        private const int NotFound = 404;
        private const int StreamNameInUse = 10058;

        private readonly string _serviceName;
        private readonly NatsJetStreamOptions _options;

        public JetStreamTopology(string serviceName, string instanceId, NatsJetStreamOptions options)
        {
            _serviceName = serviceName;
            _options = options;
            Commands = new Destination($"{serviceName}_Commands", $"{serviceName}.Commands", serviceName, StreamConfigRetention.Workqueue, ownedByBus: false);
            Requests = new Destination($"{serviceName}_Requests", $"{serviceName}.Requests", serviceName, StreamConfigRetention.Workqueue, ownedByBus: false);
            // Messages on these stay only while a bus's consumer still has to take them
            Events = new Destination($"{serviceName}_Events", $"{serviceName}.Events", instanceId, StreamConfigRetention.Interest, ownedByBus: true);
            Responses = new Destination($"{serviceName}_Responses", $"{serviceName}.Responses.{instanceId}", instanceId, StreamConfigRetention.Interest,
                ownedByBus: true, streamSubject: $"{serviceName}.Responses.*.*");
            DeadLetters = $"{serviceName}_DeadLetters";
        }

        public Destination Commands { get; }
        public Destination Requests { get; }
        public Destination Events { get; }
        // Only this bus's responses: the subject prefix its requests ask for responses on
        public Destination Responses { get; }
        public string DeadLetters { get; }

        // orders.Commands.PlaceOrder is dead-lettered as orders.DeadLetters.Commands.PlaceOrder
        public static string DeadLetterSubject(string serviceName, string subject)
        {
            return $"{serviceName}.DeadLetters.{subject.Substring(serviceName.Length + 1)}";
        }

        public static NatsConnection Connect(NatsJetStreamOptions options, string name)
        {
            return new NatsConnection(new NatsOpts { Url = options.Url, Name = name });
        }

        // Creates the streams that don't exist yet, leaving existing ones as they are, and the consumers this bus reads.
        // Another bus of the service may create them at the same time.
        public async Task EnsureCreatedAsync(INatsJSContext jetStream, bool isServer, bool isClient)
        {
            try
            {
                foreach (var stream in Streams())
                {
                    await EnsureStreamAsync(jetStream, stream).ConfigureAwait(false);
                }
            }
            catch (NatsNoRespondersException)
            {
                throw new ObvsConfigurationException(new[]
                {
                    $"Service '{_serviceName}' uses NATS JetStream, but the server at {_options.Url} doesn't have JetStream enabled. " +
                    "Start nats-server with -js, or use UseNats(...) for core NATS.",
                });
            }

            if (isServer)
            {
                await CreateConsumerAsync(jetStream, Commands).ConfigureAwait(false);
                await CreateConsumerAsync(jetStream, Requests).ConfigureAwait(false);
            }
            if (isClient)
            {
                await CreateConsumerAsync(jetStream, Events).ConfigureAwait(false);
                await CreateConsumerAsync(jetStream, Responses).ConfigureAwait(false);
            }
        }

        // Best effort: a consumer that can't be deleted, as when the server can't be reached, deletes itself once idle
        public async Task DeleteBusConsumersAsync(INatsJSContext jetStream, CancellationToken cancellationToken)
        {
            foreach (var destination in new[] { Events, Responses })
            {
                try
                {
                    await jetStream.DeleteConsumerAsync(destination.Stream, destination.Consumer, cancellationToken).ConfigureAwait(false);
                }
                catch (NatsJSApiException exception) when (exception.Error.Code == NotFound)
                {
                }
                catch (Exception exception) when (exception is NatsException || exception is OperationCanceledException)
                {
                    return;
                }
            }
        }

        private IEnumerable<StreamConfig> Streams()
        {
            yield return Commands.StreamConfig();
            yield return Requests.StreamConfig();
            yield return Events.StreamConfig();
            yield return Responses.StreamConfig();
            yield return new StreamConfig(DeadLetters, new[] { $"{_serviceName}.DeadLetters.>" }) { Retention = StreamConfigRetention.Limits };
        }

        private static async Task EnsureStreamAsync(INatsJSContext jetStream, StreamConfig stream)
        {
            try
            {
                await jetStream.GetStreamAsync(stream.Name).ConfigureAwait(false);
                return;
            }
            catch (NatsJSApiException exception) when (exception.Error.Code == NotFound)
            {
            }

            try
            {
                await jetStream.CreateStreamAsync(stream).ConfigureAwait(false);
            }
            catch (NatsJSApiException exception) when (exception.Error.ErrCode == StreamNameInUse)
            {
            }
        }

        // Creating a consumer that exists updates it to these settings
        public async Task<INatsJSConsumer> CreateConsumerAsync(INatsJSContext jetStream, Destination destination,
            CancellationToken cancellationToken = default)
        {
            var ownedByBus = destination.OwnedByBus;
            var consumer = new ConsumerConfig(destination.Consumer)
            {
                DurableName = destination.Consumer,
                FilterSubject = destination.Subjects,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                MaxDeliver = _options.MaxDeliveryAttempts,
                // A bus's own consumer starts with the messages published once it exists, as a new subscription would
                DeliverPolicy = ownedByBus ? ConsumerConfigDeliverPolicy.New : ConsumerConfigDeliverPolicy.All,
            };
            if (_options.AckWait is TimeSpan ackWait)
            {
                consumer.AckWait = ackWait;
            }
            if (ownedByBus)
            {
                consumer.InactiveThreshold = BusConsumerInactiveThreshold;
            }
            return await jetStream.CreateOrUpdateConsumerAsync(destination.Stream, consumer, cancellationToken).ConfigureAwait(false);
        }

        public sealed class Destination
        {
            private readonly StreamConfigRetention _retention;
            private readonly string _streamSubject;

            public Destination(string stream, string subjectPrefix, string consumer, StreamConfigRetention retention, bool ownedByBus,
                string streamSubject = null)
            {
                Stream = stream;
                SubjectPrefix = subjectPrefix;
                Consumer = consumer;
                OwnedByBus = ownedByBus;
                _retention = retention;
                _streamSubject = streamSubject ?? Subjects;
            }

            public string Stream { get; }
            public string SubjectPrefix { get; }
            public string Consumer { get; }
            // A bus's own consumer, rather than one the service's buses share
            public bool OwnedByBus { get; }
            public string Subjects => $"{SubjectPrefix}.*";

            public StreamConfig StreamConfig() => new StreamConfig(Stream, new[] { _streamSubject }) { Retention = _retention };
        }
    }
}
