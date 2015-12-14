using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using kafka4net;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.Kafka
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private readonly KafkaConfiguration _kafkaConfiguration;
        private readonly string _topic;
        private readonly KafkaProducerConfiguration _producerConfig;
        private readonly IMessageSerializer _serializer;
        private readonly IMessagePropertyProvider<TMessage> _propertyProvider;

        private IDisposable _disposable;
        private bool _disposed;
        private long _connected;
        private Producer _producer;

        public MessagePublisher(KafkaConfiguration kafkaConfiguration, KafkaProducerConfiguration producerConfig, string topic, IMessageSerializer serializer, IMessagePropertyProvider<TMessage> propertyProvider)
        {
            _kafkaConfiguration = kafkaConfiguration;
            _topic = topic;
            _serializer = serializer;
            _propertyProvider = propertyProvider;
            _producerConfig = producerConfig;
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
            List<KeyValuePair<string, object>> properties = _propertyProvider.GetProperties(message).ToList();

            return Publish(message, properties);
        }

        private async Task Publish(TMessage message, List<KeyValuePair<string, object>> properties)
        {
            if (_disposed)
            {
                return;
            }

            await Connect();

            KafkaHeaderedMessage kafkaHeaderedMessage = new KafkaHeaderedMessage
            {
                PayloadType = message.GetType().Name,
            };

            using (MemoryStream stream = new MemoryStream())
            {
                _serializer.Serialize(stream, message);

                kafkaHeaderedMessage.Payload = stream.ToArray();
            }

            using (MemoryStream stream = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(stream, kafkaHeaderedMessage);

                _producer.Send(new Message { Value = stream.ToArray() });
            }
        }

        private async Task Connect()
        {
            if (Interlocked.CompareExchange(ref _connected, 1, 0) == 0)
            {
                var producerConfiguration = new ProducerConfiguration(_topic,
                    batchFlushTime: TimeSpan.Zero,
                    batchFlushSize: _producerConfig.BatchFlushSize,
                    requiredAcks: 1,
                    autoGrowSendBuffers: true,
                    sendBuffersInitialSize: 200,
                    maxMessageSetSizeInBytes: 1073741824,
                    producerRequestTimeout: null,
                    partitioner: null);

                _producer = new Producer(_kafkaConfiguration.SeedAddresses, producerConfiguration);

                await _producer.ConnectAsync();

                _disposable = Disposable.Create(() =>
                {
                    _disposed = true;
                    _producer.CloseAsync(TimeSpan.FromSeconds(2)).Wait();
                });
            }
        }

        public void Dispose()
        {
            if (_disposable != null)
            {
                _disposable.Dispose();
            }
        }
    }
}