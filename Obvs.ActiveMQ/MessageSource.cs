using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Apache.NMS;
using Obvs.ActiveMQ.Extensions;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.ActiveMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        private readonly string _selector;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly IDestination _destination;
        private readonly Apache.NMS.AcknowledgementMode _mode;
        private readonly Lazy<IConnection> _lazyConnection;

        public MessageSource(Lazy<IConnection> lazyConnection,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            IDestination destination,
            AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge,
            string selector = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _lazyConnection = lazyConnection;
            _destination = destination;
            _mode = mode == AcknowledgementMode.ClientAcknowledge ? Apache.NMS.AcknowledgementMode.ClientAcknowledge : Apache.NMS.AcknowledgementMode.AutoAcknowledge;
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
                        .Select(ProcessAMQMessage)
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

        protected bool IsCorrectType(IMessage message)
        {
            return !HasTypeName(message) || _deserializers.ContainsKey(GetTypeName(message));
        }

        private TMessage ProcessAMQMessage(IMessage message)
        {
            IMessageDeserializer<TMessage> deserializer = HasTypeName(message)
                ? _deserializers[GetTypeName(message)]
                : _deserializers.Values.Single();

            var deserializedMessage = DeserializeMessage(message, deserializer);

            Acknowledge(message);

            return deserializedMessage;
        }

        protected virtual TMessage DeserializeMessage(IMessage message, IMessageDeserializer<TMessage> deserializer)
        {
            TMessage deserializedMessage = default(TMessage);

            IBytesMessage bytesMessage = message as IBytesMessage;
            if (bytesMessage != null)
            {
                using (MemoryStream ms = new MemoryStream(bytesMessage.Content))
                {
                    deserializedMessage = deserializer.Deserialize(ms);
                }
            }

            return deserializedMessage;
        }

        protected string GetTypeName(IMessage message)
        {
            return message.Properties.GetString(MessagePropertyNames.TypeName);
        }

        protected bool HasTypeName(IMessage message)
        {
            return message.Properties.Contains(MessagePropertyNames.TypeName);
        }

        private void Acknowledge(IMessage message)
        {
            if (_mode != Apache.NMS.AcknowledgementMode.AutoAcknowledge)
            {
                message.Acknowledge();
            }
        }

        public void Dispose()
        {
        }
    }
}