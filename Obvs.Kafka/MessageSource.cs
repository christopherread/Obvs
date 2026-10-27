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
using Microsoft.Extensions.Logging;
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
        private readonly ILogger _logger;

        // How long Subscribe waits for Kafka to assign partitions, and for each offset query while it does
        private static readonly TimeSpan PartitionAssignmentTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan OffsetQueryTimeout = TimeSpan.FromSeconds(10);

        /// <param name="groupId">The consumer group. Without one, the group is named after the process, so each process receives every message</param>
        public MessageSource(KafkaConfiguration kafkaConfig,
            KafkaSourceConfiguration sourceConfig, 
            string topicName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> propertyFilter,
            string groupId = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _topicName = topicName;
            _propertyFilter = propertyFilter;
            var clientId = $"{Environment.MachineName}-{Environment.UserName}-{Process.GetCurrentProcess().Id}";
            _consumerConfig = new ConsumerConfig
            {
                ClientId = clientId,
                GroupId = string.IsNullOrEmpty(groupId) ? clientId : groupId,
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
            _logger = kafkaConfig.Logger;
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
                // Cancelled when the subscription ends, or by the error handler, which runs on this thread as a side
                // effect of Consume, when an error ends it. Consume then stops waiting for a message.
                using (var consuming = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    var builder = new ConsumerBuilder<Ignore, WrapperMessage>(_consumerConfig)
                        .SetValueDeserializer(new ConsumerValueDeserializer<WrapperMessage>())
                        .SetErrorHandler((_, error) =>
                        {
                            if (OnClientError(error, observer))
                            {
                                consuming.Cancel();
                            }
                        })
                        .SetPartitionsAssignedHandler((c, partitions) =>
                        {
                            var offsets = StartOffsets(c, partitions, _logger);
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
                            while (!consuming.IsCancellationRequested && ConsumeMessage(consuming.Token, observer, consumer))
                            {
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
        // The end of the log is committed straight away, so a bus that takes the partition over, or this one
        // restarted, carries on from there even if nothing was consumed, rather than skipping to the end again.
        private static IEnumerable<TopicPartitionOffset> StartOffsets(IConsumer<Ignore, WrapperMessage> consumer, List<TopicPartition> partitions, ILogger logger)
        {
            var committed = consumer.Committed(partitions, OffsetQueryTimeout);
            var fromEnd = committed
                .Where(offset => offset.Offset == Offset.Unset)
                .Select(offset => new TopicPartitionOffset(offset.TopicPartition, consumer.QueryWatermarkOffsets(offset.TopicPartition, OffsetQueryTimeout).High))
                .ToList();
            if (fromEnd.Count > 0)
            {
                try
                {
                    consumer.Commit(fromEnd);
                }
                catch (KafkaException exception)
                {
                    // This consumer still starts from there; only a later one might not
                    logger.LogWarning(exception, "Kafka consumer couldn't commit its start position: {Reason}", exception.Error.Reason);
                }
            }
            return committed.Where(offset => offset.Offset != Offset.Unset).Concat(fromEnd).ToList();
        }

        // Returns false once the subscription has ended
        private bool ConsumeMessage(CancellationToken token, IObserver<TMessage> observer, IConsumer<Ignore, WrapperMessage> consumer)
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
                return true;
            }
            catch (ConsumeException exception)
            {
                // Including a message that isn't an Obvs message, which has been consumed and is skipped
                return !OnClientError(exception, observer);
            }
            catch (Exception exception)
            {
                observer.OnError(exception);
                return false;
            }
        }

        // Returns true when the error ends the subscription
        internal bool OnClientError(Error error, IObserver<TMessage> observer)
        {
            return OnClientError(new KafkaException(error), observer);
        }

        // librdkafka recovers by itself from the errors it reports, such as brokers that are down or a failed OAUTHBEARER
        // token refresh, unless they're fatal. Being refused access to a topic, the group or the cluster doesn't fix
        // itself either. Only those end the subscription; the rest are logged and consumption carries on.
        private bool OnClientError(KafkaException exception, IObserver<TMessage> observer)
        {
            var error = exception.Error;
            if (error.IsFatal
                || error.Code == ErrorCode.TopicAuthorizationFailed
                || error.Code == ErrorCode.GroupAuthorizationFailed
                || error.Code == ErrorCode.ClusterAuthorizationFailed)
            {
                observer.OnError(exception);
                return true;
            }

            _logger.LogWarning(exception, "Kafka consumer of {Topic} reported an error, and carries on: {Reason}", _topicName, error.Reason);
            return false;
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