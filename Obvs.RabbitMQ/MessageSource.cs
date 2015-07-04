using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
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
                    var consumer = _channel.CreateConsumer(_exchange, _routingKey);

                    var subscription = consumer.GetMessages()
                                               .Select(Deserialize)
                                               .Subscribe(observer);

                    return Disposable.Create(() =>
                    {
                        subscription.Dispose();
                        consumer.Queue.Close();
                    });
                });
            }
        }

        private TMessage Deserialize(BasicDeliverEventArgs deliverEventArgs)
        {
            var deserializer = GetDeserializer(deliverEventArgs);

            byte[] body = deliverEventArgs.Body;
            string contentType = deliverEventArgs.BasicProperties.ContentType;

            return contentType == "text" ? deserializer.Deserialize(Encoding.UTF8.GetString(body)) : 
                                           deserializer.Deserialize(body);
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
