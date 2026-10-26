using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Obvs.Hosting;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;
using ProtoBuf;
using SerializationContext = Confluent.Kafka.SerializationContext;

namespace Obvs.Kafka
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> 
        where TMessage : class
    {
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly string _topicName;
        private readonly Func<Dictionary<string, string>, bool> _propertyFilter;

        private readonly ConsumerConfig _consumerConfig;
        private readonly Action<IClient, string> _oauthBearerTokenRefreshHandler;

        // How long Subscribe waits for Kafka to assign partitions, and for each offset query while it does
        private static readonly TimeSpan PartitionAssignmentTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan OffsetQueryTimeout = TimeSpan.FromSeconds(10);

        /// <param name="uniqueGroupSuffix">Only required if you intend to create two or more message sources in the same process</param>
        public MessageSource(KafkaConfiguration kafkaConfig,
            KafkaSourceConfiguration sourceConfig, 
            string topicName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> propertyFilter,
            string uniqueGroupSuffix = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _topicName = topicName;
            _propertyFilter = propertyFilter;
            var clientId = $"{Environment.MachineName}-{Environment.UserName}-{Process.GetCurrentProcess().Id}";
            _consumerConfig = new ConsumerConfig
            {
                ClientId = clientId,
                // A group of its own, so it receives every message and other sources' subscriptions don't rebalance it
                GroupId = string.IsNullOrEmpty(uniqueGroupSuffix) ? clientId : $"{clientId}-{topicName}-{uniqueGroupSuffix}",
                BootstrapServers = kafkaConfig.BootstrapServers,
                FetchMinBytes = sourceConfig.FetchMinBytes,
                FetchMaxBytes = sourceConfig.FetchMaxBytes,
                EnableAutoCommit = true,
                StatisticsIntervalMs = 5000,
                SessionTimeoutMs = 6000,
                AutoOffsetReset = AutoOffsetReset.Latest,
            };
            kafkaConfig.ApplyTo(_consumerConfig);
            _oauthBearerTokenRefreshHandler = kafkaConfig.OAuthBearerTokenRefreshHandler;
        }

        internal ConsumerConfig ConsumerConfig => _consumerConfig;

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var tokenSource = new CancellationTokenSource();
                    var token = tokenSource.Token;
                    var subscribedEvent = new ManualResetEventSlim(false);

                    var task = Task.Run(() => Subscriber(token, observer, subscribedEvent));

                    // Wait until partitions are assigned, so messages published once Subscribe returns are received
                    subscribedEvent.Wait(PartitionAssignmentTimeout);

                    return Disposable.Create(() =>
                    {
                        tokenSource.Cancel();
                        task.Wait();
                    });
                })
                // Hand messages to subscribers on the task pool, in order. Subscriber code running on the consume
                // thread could otherwise deadlock it, for example by disposing the bus, which waits for that thread.
                .ObserveOn(TaskPoolScheduler.Default);
            }
        }

        private void Subscriber(CancellationToken token, IObserver<TMessage> observer, ManualResetEventSlim subscribedEvent)
        {
            try
            {
                var builder = new ConsumerBuilder<Ignore, WrapperMessage>(_consumerConfig)
                    .SetValueDeserializer(new ConsumerValueDeserializer<WrapperMessage>())
                    .SetErrorHandler((_, e) => observer.OnError(new Exception($"Consumer ErrorHandler: {e.Reason}")))
                    .SetPartitionsAssignedHandler((c, partitions) =>
                    {
                        var offsets = StartOffsets(c, partitions);
                        subscribedEvent.Set();
                        return offsets;
                    });
                if (_oauthBearerTokenRefreshHandler != null)
                {
                    builder.SetOAuthBearerTokenRefreshHandler(_oauthBearerTokenRefreshHandler);
                }

                using (var consumer = builder.Build())
                {
                    consumer.Subscribe(_topicName);

                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            ConsumeMessage(token, observer, consumer);
                        }
                    }
                    catch (Exception exception)
                    {
                        observer.OnError(exception);
                    }
                    finally
                    {
                        consumer.Unsubscribe();
                        consumer.Close();
                    }
                }
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }
            finally
            {
                if (!subscribedEvent.IsSet)
                {
                    subscribedEvent.Set();
                }
            }
        }

        // Fix each partition's start position at assignment: the group's committed offset, or else the current
        // end of the log. Leaving "latest" to be resolved later would skip messages published in the meantime.
        private static IEnumerable<TopicPartitionOffset> StartOffsets(IConsumer<Ignore, WrapperMessage> consumer, List<TopicPartition> partitions)
        {
            return consumer.Committed(partitions, OffsetQueryTimeout)
                .Select(committed => committed.Offset != Offset.Unset
                    ? committed
                    : new TopicPartitionOffset(committed.TopicPartition, consumer.QueryWatermarkOffsets(committed.TopicPartition, OffsetQueryTimeout).High))
                .ToList();
        }

        private void ConsumeMessage(CancellationToken token, IObserver<TMessage> observer, IConsumer<Ignore, WrapperMessage> consumer)
        {
            try
            {
                // TODO: deal with nulls here?
                var kafkaMessage = consumer.Consume(token).Message;
                var msg = kafkaMessage?.Value;

                if (_propertyFilter == null || _propertyFilter(msg.Properties))
                {
                    var message = DeserializePayload(msg);
                    SetHeaders(message, kafkaMessage.Headers);
                    observer.OnNext(message);
                }
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
            }
        }

        private TMessage DeserializePayload(WrapperMessage msg)
        {
            using (var s = new MemoryStream(msg.Payload))
            {
                return _deserializers[msg.PayloadType].Deserialize(s);
            }
        }

        private static void SetHeaders(TMessage message, Headers kafkaHeaders)
        {
            if (kafkaHeaders == null || kafkaHeaders.Count == 0)
            {
                return;
            }

            var headers = new Dictionary<string, string>();
            foreach (var header in kafkaHeaders)
            {
                headers[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
            }
            ObvsMessageHeaders.Set(message, headers);
        }

        public void Dispose()
        {
        }
    }

    public class ConsumerValueDeserializer<T> : IDeserializer<T> where T : new()
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context) => 
            Serializer.Deserialize<T>(new MemoryStream(data.ToArray()));
    }
}