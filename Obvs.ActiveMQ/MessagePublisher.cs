using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Apache.NMS;
using Obvs.ActiveMQ.Extensions;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.ActiveMQ
{
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage> 
        where TMessage : class
    {
        private readonly IDestination _destination;
        private readonly IMessageSerializer _serializer;
        private readonly IMessagePropertyProvider<TMessage> _propertyProvider;
        private readonly IScheduler _scheduler;
        private readonly Func<TMessage, MsgDeliveryMode> _deliveryMode;
        private readonly Func<TMessage, MsgPriority> _priority;
        private readonly Func<TMessage, TimeSpan> _timeToLive;
        private readonly Lazy<IConnection> _connection;
        private ISession _session;
        private IMessageProducer _producer;
        private IDisposable _disposable;

        public MessagePublisher(Lazy<IConnection> lazyConnection, IDestination destination, IMessageSerializer serializer, IMessagePropertyProvider<TMessage> propertyProvider, IScheduler scheduler,
                                Func<TMessage, MsgDeliveryMode> deliveryMode = null, Func<TMessage, MsgPriority> priority = null, Func<TMessage, TimeSpan> timeToLive = null)
        {
            _connection = lazyConnection;
            _destination = destination;
            _serializer = serializer;
            _propertyProvider = propertyProvider;
            _scheduler = scheduler;
            _deliveryMode = deliveryMode ?? (message => MsgDeliveryMode.NonPersistent);
            _priority = priority ?? (message => MsgPriority.Normal);
            _timeToLive = timeToLive ?? (message => TimeSpan.Zero);
        }

        public Task PublishAsync(TMessage message)
        {
            return Observable.Start(() => Publish(message), _scheduler).ToTask();
        }

        private void Publish(TMessage message)
        {
            List<KeyValuePair<string, object>> properties = _propertyProvider.GetProperties(message).ToList();

            Publish(message, properties);
        }

        private void Publish(TMessage message, List<KeyValuePair<string, object>> properties)
        {
            Connect();

            AppendTypeNameProperty(message, properties);

            object data = _serializer.Serialize(message);

            _session.CreateMessageFromData(data)
                    .SetProperties(properties)
                    .Send(_producer, _deliveryMode(message), _priority(message), _timeToLive(message));
        }

        private static void AppendTypeNameProperty(TMessage message, List<KeyValuePair<string, object>> properties)
        {
            properties.Add(new KeyValuePair<string, object>(MessagePropertyNames.TypeName, message.GetType().Name));
        }

        private void Connect()
        {
            if (_disposable == null)
            {
                _session = _connection.Value.CreateSession(Apache.NMS.AcknowledgementMode.AutoAcknowledge);
                _producer = _session.CreateProducer(_destination);

                _disposable = Disposable.Create(() =>
                {
                    _producer.Close();
                    _producer.Dispose();
                    _session.Close();
                    _session.Dispose();
                });
            }
        }

        private void Disconnect()
        {
            if (_disposable != null)
            {
                _disposable.Dispose();
                _disposable = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}