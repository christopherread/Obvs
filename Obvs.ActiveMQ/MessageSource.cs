using System;
using System.Collections;
using System.Collections.Concurrent;
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
        private readonly bool _noLocal;

        public MessageSource(Lazy<IConnection> lazyConnection,
            IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            IDestination destination,
            AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge,
            string selector = null,
            Func<IDictionary, bool> propertyFilter = null,
            bool noLocal = false)
        {
            _deserializers = deserializers.ToDictionary(d => d.GetTypeName());
            _lazyConnection = lazyConnection;
            _destination = destination;
            _mode = mode == AcknowledgementMode.ClientAcknowledge ? Apache.NMS.AcknowledgementMode.ClientAcknowledge : Apache.NMS.AcknowledgementMode.AutoAcknowledge;
            _selector = selector;
            _propertyFilter = propertyFilter;
            _noLocal = noLocal;

            var messages = Observable.Create<TMessage>(observer =>
                {
                    var session = _lazyConnection.Value.CreateSession(_mode);

                    var subscription = session
                        .ToObservable(_destination, _selector, _noLocal)
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

            // Can only do this optimization if we're on a topic
            if (_destination.DestinationType == DestinationType.Topic)
            {
                messages = messages
                      .PublishRefCountRetriable();
            }

            Messages = messages;
        }

        public IObservable<TMessage> Messages { get; }

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

            if (!message.Properties.TryGetTypeName(out typeName))
            {
                return _deserializers.Values.SingleOrDefault();
            }

            IMessageDeserializer<TMessage> deserializer;
            return _deserializers.TryGetValue(typeName, out deserializer) ? deserializer : null;
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