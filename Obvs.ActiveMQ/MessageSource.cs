using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Apache.NMS;
using Obvs.ActiveMQ.Extensions;
using Obvs.ActiveMQ.Utils;
using Obvs.Serialization;

namespace Obvs.ActiveMQ
{
    public class MessageSource<TMessage> : IMessageSource<TMessage>
        where TMessage : class
    {
        private readonly string _selector;
        private readonly Func<IDictionary, bool> _propertyFilter;
        private readonly IDictionary<string, IMessageDeserializer<TMessage>> _deserializers;
        private readonly IDestination _destination;
        private readonly Apache.NMS.AcknowledgementMode _mode;
        private readonly Lazy<IConnection> _lazyConnection;
        
        public MessageSource(Lazy<IConnection> lazyConnection,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            IDestination destination,
            AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge,
            string selector = null,
            Func<IDictionary, bool> propertyFilter = null)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _lazyConnection = lazyConnection;
            _destination = destination;
            _mode = mode == AcknowledgementMode.ClientAcknowledge ? Apache.NMS.AcknowledgementMode.ClientAcknowledge : Apache.NMS.AcknowledgementMode.AutoAcknowledge;
            _selector = selector;
            _propertyFilter = propertyFilter;
        }

        public IObservable<TMessage> Messages
        {
            get
            {
                return Observable.Create<TMessage>(observer =>
                {
                    var session = _lazyConnection.Value.CreateSession(_mode);

                    var subscription = session.ToObservable(_destination, _selector)
                        .Where(PassesFilter)
                        .Select(message => new { message, deserializer = GetDeserializer(message) })
                        .Where(msg => msg.deserializer != null)
                        .Select(msg => DeserializeAndAcknowledge(msg.message, msg.deserializer))
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
            return _propertyFilter == null ||
                   _propertyFilter(new PrimitiveMapDictionary(message.Properties));
        }

        private TMessage DeserializeAndAcknowledge(IMessage message, IMessageDeserializer<TMessage> deserializer)
        {
            var deserializedMessage = DeserializeMessage(message, deserializer);
            Acknowledge(message);
            return deserializedMessage;
        }

        private IMessageDeserializer<TMessage> GetDeserializer(IMessage message)
        {
            string typeName;
            IMessageDeserializer<TMessage> deserializer;

            return message.TryGetTypeName(out typeName)
                ? _deserializers.TryGetValue(typeName, out deserializer)
                    ? deserializer
                    : _deserializers.Values.SingleOrDefault()
                : null;
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