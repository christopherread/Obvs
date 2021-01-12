using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
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

        public MessageSource(KafkaConfiguration kafkaConfig,
            KafkaSourceConfiguration sourceConfig, 
            string topicName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> propertyFilter)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _topicName = topicName;
            _propertyFilter = propertyFilter;
            var clientId = $"{Environment.MachineName}-{Environment.UserName}-{Process.GetCurrentProcess().Id}";
            _consumerConfig = new ConsumerConfig
            {
                ClientId = clientId,
                GroupId = clientId,
                BootstrapServers = kafkaConfig.BootstrapServers,
                FetchMinBytes = sourceConfig.FetchMinBytes,
                FetchMaxBytes = sourceConfig.FetchMaxBytes,
                EnableAutoCommit = true,
                StatisticsIntervalMs = 5000,
                SessionTimeoutMs = 6000,
                AutoOffsetReset = AutoOffsetReset.Latest,
            };
        }

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

                    subscribedEvent.Wait();

                    return Disposable.Create(() =>
                    {
                        tokenSource.Cancel();
                        task.Wait();
                    });
                });
            }
        }

        private void Subscriber(CancellationToken token, IObserver<TMessage> observer, ManualResetEventSlim subscribedEvent)
        {
            try
            {
                using (var consumer = new ConsumerBuilder<Ignore, WrapperMessage>(_consumerConfig)
                    .SetValueDeserializer(new ConsumerValueDeserializer<WrapperMessage>())
                    .SetErrorHandler((_, e) => observer.OnError(new Exception($"Consumer ErrorHandler: {e.Reason}")))
                    .Build())
                {
                    consumer.Subscribe(_topicName);
                    subscribedEvent.Set();

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

        private void ConsumeMessage(CancellationToken token, IObserver<TMessage> observer, IConsumer<Ignore, WrapperMessage> consumer)
        {
            try
            {
                // TODO: deal with nulls here?
                var msg = consumer.Consume(token).Message?.Value;

                if (_propertyFilter == null || _propertyFilter(msg.Properties))
                {
                    observer.OnNext(DeserializePayload(msg));
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