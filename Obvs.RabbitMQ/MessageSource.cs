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
        private readonly string _exchange;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _routingKey;

        public MessageSource(IConnectionFactory connectionFactory, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string exchange, string routingKeyPrefix)
        {
            _exchange = exchange;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());

            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, ExchangeType.Topic);
            _routingKey = string.Format("{0}.*", routingKeyPrefix);
        }

        public void Dispose()
        {
            _channel.Close();
            _channel.Dispose();
            _connection.Close();
            _connection.Dispose();
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var queue = _channel.QueueDeclare();
                    _channel.QueueBind(queue, _exchange, _routingKey);
                    var consumer = new EventingBasicConsumer(_channel);
                    
                    var subscription = consumer.ToObservable()
                                               .Select(Deserialize)
                                               .Subscribe(observer);

                    _channel.BasicConsume(queue, true, consumer);

                    return Disposable.Create(() =>
                    {
                        subscription.Dispose();
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
            string typeName = deliverEventArgs.RoutingKey.Split('.').LastOrDefault();

            return _deserializers.ContainsKey(typeName)
                ? _deserializers[typeName]
                : _deserializers.Values.Single();
        }
    }
}
