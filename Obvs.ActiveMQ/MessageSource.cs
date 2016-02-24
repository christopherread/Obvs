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
        private readonly Func<List<KeyValuePair<string, string>>, bool> _messagePropertyFilter;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly IDestination _destination;
        private readonly Apache.NMS.AcknowledgementMode _mode;
        private readonly Lazy<IConnection> _lazyConnection;

        public MessageSource(Lazy<IConnection> lazyConnection,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            IDestination destination,
            AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge,
            string selector = null,
            Func<List<KeyValuePair<string, string>>, bool> messagePropertyFilter = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _lazyConnection = lazyConnection;
            _destination = destination;
            _mode = mode == AcknowledgementMode.ClientAcknowledge ? Apache.NMS.AcknowledgementMode.ClientAcknowledge : Apache.NMS.AcknowledgementMode.AutoAcknowledge;
            _selector = selector;
            _messagePropertyFilter = messagePropertyFilter;
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var session = _lazyConnection.Value.CreateSession(_mode);

                    var subscription = session.ToObservable(_destination, _selector)
                        .Where(message => IsCorrectType(message) &&
                                          PassesFilter(message))
                        .Select(ProcessMessage)
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

        private bool PassesFilter(IMessage message)
        {
            return _messagePropertyFilter == null ||
                   _messagePropertyFilter(message.GetProperties());
        }

        protected bool IsCorrectType(IMessage message)
        {
            return !HasTypeName(message) || _deserializers.ContainsKey(GetTypeName(message));
        }

        private TMessage ProcessMessage(IMessage message)
        {
            var deserializer = GetDeserializer(message);
            var deserializedMessage = DeserializeMessage(message, deserializer);
            Acknowledge(message);
            return deserializedMessage;
        }

        private IMessageDeserializer<TMessage> GetDeserializer(IMessage message)
        {
            return HasTypeName(message)
                ? _deserializers[GetTypeName(message)]
                : _deserializers.Values.Single();
        }

        protected virtual TMessage DeserializeMessage(IMessage message, IMessageDeserializer<TMessage> deserializer)
        {
            var bytesMessage = message as IBytesMessage;

            if (bytesMessage == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(bytesMessage.Content))
            {
                return deserializer.Deserialize(stream);
            }
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