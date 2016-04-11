using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using kafka4net;
using kafka4net.ConsumerImpl;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;
using ProtoBuf;

namespace Obvs.Kafka
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> 
        where TMessage : class
    {
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly KafkaConfiguration _kafkaConfig;
        private readonly string _topicName;
        private readonly Func<Dictionary<string, string>, bool> _propertyFilter;

        private readonly KafkaSourceConfiguration _sourceConfig;
        private readonly bool _applyFilter;

        public MessageSource(KafkaConfiguration kafkaConfig,
            KafkaSourceConfiguration sourceConfig, 
            string topicName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            Func<Dictionary<string, string>, bool> propertyFilter)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _kafkaConfig = kafkaConfig;
            _topicName = topicName;
            _propertyFilter = propertyFilter;
            _sourceConfig = sourceConfig;
            _applyFilter = propertyFilter != null;
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var consumerConfiguration = new ConsumerConfiguration(
                        _kafkaConfig.SeedAddresses, 
                        _topicName,
                        new StartPositionTopicEnd(), // always start at end of stream - ActiveMQ topic behaviour.
                        maxWaitTimeMs: 1000,
                        minBytesPerFetch: _sourceConfig.MinBytesPerFetch,
                        maxBytesPerFetch: _sourceConfig.MaxBytesPerFetch,
                        lowWatermark: 500,
                        highWatermark: 2000,
                        useFlowControl: false,
                        stopPosition: null,
                        scheduler: Scheduler.Default);

                    var consumer = new Consumer(consumerConfiguration);

                    return consumer
                        .OnMessageArrived
                        .Select(DeserializeMessage)
                        .Where(PassesFilter)
                        .Select(DeserializePayload)
                        .Subscribe(observer);
                });
            }
        }

        private static KafkaHeaderedMessage DeserializeMessage(ReceivedMessage message)
        {
            return Serializer.Deserialize<KafkaHeaderedMessage>(KafkaHeaderedMessage.ToStream(message.Value));
        }

        private bool PassesFilter(KafkaHeaderedMessage message)
        {
            return !_applyFilter || _propertyFilter(message.Properties);
        }

        private TMessage DeserializePayload(KafkaHeaderedMessage message)
        {
            var deserializer = _deserializers[message.PayloadType];
            return deserializer.Deserialize(new MemoryStream(message.Payload));
        }

        public void Dispose()
        {
        }
    }
}