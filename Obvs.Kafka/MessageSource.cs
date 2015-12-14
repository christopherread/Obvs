using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using kafka4net;
using kafka4net.ConsumerImpl;
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

        private readonly KafkaSourceConfiguration _sourceConfig;

        public MessageSource(KafkaConfiguration kafkaConfig,
            KafkaSourceConfiguration sourceConfig, 
            string topicName,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _kafkaConfig = kafkaConfig;
            _topicName = topicName;
            _sourceConfig = sourceConfig;
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
                        .Select(Deserialize)
                        .Subscribe(observer);
                });
            }
        }


        private TMessage Deserialize(ReceivedMessage message)
        {
            KafkaHeaderedMessage headeredMessage = Serializer.Deserialize<KafkaHeaderedMessage>(KafkaHeaderedMessage.ToStream(message.Value));

            IMessageDeserializer<TMessage> deserializer = _deserializers[headeredMessage.PayloadType];

            TMessage deserializedMessage = deserializer.Deserialize(new MemoryStream(headeredMessage.Payload));

            return deserializedMessage;
        }

        public void Dispose()
        {
        }
    }
}