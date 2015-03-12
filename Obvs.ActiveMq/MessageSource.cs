using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Apache.NMS;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMq
{
    public class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : IMessage
    {
        private readonly string _selector;
        private readonly IConnectionFactory _connectionFactory;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly IDestination _destination;
        private readonly AcknowledgementMode _mode;
        private readonly Lazy<IConnection> _lazyConnection;

        public MessageSource(IConnectionFactory connectionFactory, IEnumerable<IMessageDeserializer<TMessage>> deserializers, IDestination destination, AcknowledgementMode mode)
        {
            _connectionFactory = connectionFactory;
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _destination = destination;
            _mode = mode;
            
            _lazyConnection = new Lazy<IConnection>(() =>
            {
                IConnection connection = _connectionFactory.CreateConnection();
                connection.Start();
                return connection;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public MessageSource(IConnectionFactory connectionFactory,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers, IDestination destination,
            AcknowledgementMode mode, string selector)
            : this(connectionFactory, deserializers, destination, mode)
        {
            _selector = selector;
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    ISession session = _lazyConnection.Value.CreateSession(_mode);

                    IDisposable subscription = session.ToObservable(_destination, _selector)
                        .Where(IsCorrectType)
                        .Select(Deserialize)
                        .Subscribe(observer);

                    return Disposable.Create(() =>
                    {
                        subscription.Dispose();
                        session.Close();
                        session.Dispose();
                    });
                });
            }
        }

        private bool IsCorrectType(Apache.NMS.IMessage message)
        {
            return !HasTypeName(message) || _deserializers.ContainsKey(GetTypeName(message));
        }

        private TMessage Deserialize(Apache.NMS.IMessage message)
        {
            IMessageDeserializer<TMessage> deserializer = HasTypeName(message) ? _deserializers[GetTypeName(message)] : _deserializers.Values.Single();

            TMessage deserializedMessage = default(TMessage);

            ITextMessage textMessage = message as ITextMessage;
            if (textMessage != null)
            {
                deserializedMessage = deserializer.Deserialize(textMessage.Text);
            }
            else
            {
                IBytesMessage bytesMessage = message as IBytesMessage;
                if (bytesMessage != null)
                {
                    deserializedMessage = deserializer.Deserialize(bytesMessage.Content);
                }
            }
            Acknowledge(message);

            return deserializedMessage;
        }

        private static string GetTypeName(Apache.NMS.IMessage message)
        {
            return message.Properties.GetString(MessagePropertyNames.TypeName);
        }

        private static bool HasTypeName(Apache.NMS.IMessage message)
        {
            return message.Properties.Contains(MessagePropertyNames.TypeName);
        }

        private void Acknowledge(Apache.NMS.IMessage message)
        {
            if (_mode != AcknowledgementMode.AutoAcknowledge)
            {
                message.Acknowledge();
            }
        }

        public void Dispose()
        {
            if (_lazyConnection.IsValueCreated)
            {
                _lazyConnection.Value.Stop();
                _lazyConnection.Value.Close();
                _lazyConnection.Value.Dispose();
            }
        }
    }
}