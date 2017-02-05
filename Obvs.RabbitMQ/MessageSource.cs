using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Obvs.RabbitMQ.Extensions;
using Obvs.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : class
    {
        private readonly Lazy<IConnection> _connection;
        private readonly string _exchange;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly string _routingKey;

        public MessageSource(Lazy<IConnection> connection, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string exchange, string routingKeyPrefix)
        {
            _connection = connection;
            _exchange = exchange;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _routingKey = string.Format("{0}.*", routingKeyPrefix);
        }

        public void Dispose()
        {
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var channel = _connection.Value.CreateModel();
                    channel.ExchangeDeclare(_exchange, ExchangeType.Topic);

                    var queue = channel.QueueDeclare();
                    channel.QueueBind(queue, _exchange, _routingKey);

                    var consumer = new EventingBasicConsumer(channel);
                    
                    var subscription = consumer.ToObservable()
                                               .Select(Deserialize)
                                               .Subscribe(observer);

                    channel.BasicConsume(queue, true, consumer);

                    return Disposable.Create(() =>
                    {
                        subscription.Dispose();
                        channel.BasicCancel(consumer.ConsumerTag);
                        channel.QueueUnbind(queue, _exchange, _routingKey);
                        channel.QueueDelete(queue);
                        channel.Close();
                        channel.Dispose();
                    });
                });
            }
        }

        private TMessage Deserialize(BasicDeliverEventArgs deliverEventArgs)
        {
            var deserializer = GetDeserializer(deliverEventArgs);
            return deserializer.Deserialize(deliverEventArgs.Body); 
        }

        private IMessageDeserializer<TMessage> GetDeserializer(BasicDeliverEventArgs deliverEventArgs)
        {
            var typeName = deliverEventArgs.RoutingKey.Split('.').LastOrDefault();

            if (typeName == null)
            {
                throw new Exception(string.Format("Unable to parse typeName from RoutingKey '{0}'", deliverEventArgs.RoutingKey));
            }

            IMessageDeserializer<TMessage> deserializer;
            if (_deserializers.TryGetValue(typeName, out deserializer))
            {
                return deserializer;
            }

            if (_deserializers.Count == 1)
            {
                return _deserializers.Single().Value;
            }

            throw new Exception(string.Format("Unable to find deserializer for typeName '{0}'", typeName));
        }
    }
}
