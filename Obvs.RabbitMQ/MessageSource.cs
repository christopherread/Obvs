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
        private readonly Lazy<IModel> _channel;
        private readonly string _routingKey;

        public MessageSource(Lazy<IModel> channel, IEnumerable<IMessageDeserializer<TMessage>> deserializers, string exchange, string routingKeyPrefix)
        {
            _channel = channel;
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
                    var queue = _channel.Value.QueueDeclare();
                    _channel.Value.QueueBind(queue, _exchange, _routingKey);
                    var consumer = new EventingBasicConsumer(_channel.Value);
                    
                    var subscription = consumer.ToObservable()
                                               .Select(Deserialize)
                                               .Subscribe(observer);

                    _channel.Value.BasicConsume(queue, true, consumer);

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
