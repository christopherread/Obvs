using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using Obvs.Types;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Obvs.RabbitMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage> where TMessage : IMessage
    {
        private readonly string _exchange;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly string _routingKey;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public MessageSource(IConnectionFactory connectionFactory, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string exchange)
        {
            _exchange = exchange;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());

            _connection = connectionFactory.CreateConnection();
            _routingKey = typeof(TMessage).Name + ".*";
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_exchange, "topic");
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
                    var queueName = _channel.QueueDeclare();
                    _channel.QueueBind(queueName, _exchange, _routingKey);

                    var consumer = new QueueingBasicConsumer(_channel);
                    _channel.BasicConsume(queueName, true, consumer);

                    return Observable.Timer(TimeSpan.Zero, TimeSpan.Zero)
                        .Select(i => consumer.Queue.Dequeue())
                        .Select(Deserialize)
                        .Subscribe(observer);
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
            string typeName = deliverEventArgs.RoutingKey.Substring(deliverEventArgs.RoutingKey.LastIndexOf(".", StringComparison.Ordinal) + 1);

            return  _deserializers.ContainsKey(typeName)
                ? _deserializers[typeName]
                : _deserializers.Values.Single();
        }
    }
}
