using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Obvs.Kafka.Configuration;
using Obvs.Serialization;

namespace Obvs.Kafka
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private readonly string _topic;
        private readonly ProducerConfig _producerConfig;
        private readonly IMessageSerializer _serializer;
        private readonly Func<TMessage, Dictionary<string, string>> _propertyProvider;

        private IDisposable _disposable;
        private bool _disposed;
        private long _created;
        private IProducer<Null, WrapperMessage> _producer;

        public MessagePublisher(KafkaConfiguration kafkaConfiguration, KafkaProducerConfiguration producerConfig, string topic, IMessageSerializer serializer, Func<TMessage, Dictionary<string, string>> propertyProvider)
        {
            _topic = topic;
            _serializer = serializer;
            _propertyProvider = propertyProvider;
            _producerConfig = new ProducerConfig { 
                BootstrapServers = kafkaConfiguration.BootstrapServers,
                BatchNumMessages = producerConfig.BatchFlushSize
            };
        }

        public Task PublishAsync(TMessage message)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Publisher has been disposed already.");
            }

            return Publish(message);
        }

        private Task Publish(TMessage message)
        {
            var properties = _propertyProvider?.Invoke(message);

            return Publish(message, properties);
        }

        private async Task Publish(TMessage message, Dictionary<string, string> properties)
        {
            if (_disposed)
            {
                return;
            }

            CreateProducer();

            var wrapperMessage = ToWrapperMessage(message, properties);
            var msg = new Message<Null, WrapperMessage> {Value = wrapperMessage};

            await _producer.ProduceAsync(_topic, msg);
        }

        private WrapperMessage ToWrapperMessage(TMessage message, Dictionary<string, string> properties)
        {
            using (var stream = new MemoryStream())
            {
                _serializer.Serialize(stream, message);
                return new WrapperMessage
                {
                    PayloadType = message.GetType().Name,
                    Properties = properties,
                    Payload = stream.ToArray()
                };
            }
        }

        private void CreateProducer()
        {
            if (Interlocked.CompareExchange(ref _created, 1, 0) == 0)
            {
                _producer = new ProducerBuilder<Null, WrapperMessage>(_producerConfig)
                    .SetValueSerializer(new ProducerValueSerializer<WrapperMessage>())
                    .Build();

                _disposable = Disposable.Create(() =>
                {
                    _disposed = true;
                    _producer.Flush();
                    _producer.Dispose();
                });
            }
        }

        public void Dispose()
        {
            if (_disposable != null && !_disposed)
            {
                _disposable.Dispose();
            }
        }
    }

    public class ProducerValueSerializer<T> : ISerializer<T>
    {
        public byte[] Serialize(T data, SerializationContext context)
        {
            using (var stream = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(stream, data);
                return stream.ToArray();
            }
        }
    }
}