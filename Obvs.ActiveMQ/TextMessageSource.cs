using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Apache.NMS;
using Obvs.Serialization;

namespace Obvs.ActiveMQ
{
    /// <summary>
    /// MessageSource implementation that deserializes ActiveMQ ITextMessages using supplied Encoding constructor parameter
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class TextMessageSource<TMessage> : MessageSource<TMessage> where TMessage : class
    {
        private readonly Encoding _encoding;

        public TextMessageSource(Lazy<IConnection> lazyConnection, IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            IDestination destination, AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge, string selector = null, 
            Encoding encoding = null, Func<IDictionary, bool> propertyFilter = null, bool noLocal = false)
            : base(lazyConnection, deserializers, destination, mode, selector, propertyFilter, noLocal)
        {
            _encoding = encoding ?? Encoding.UTF8;
        }

        protected override TMessage DeserializeMessage(IMessage message, IMessageDeserializer<TMessage> deserializer)
        {
            var textMessage = message as ITextMessage;

            if (textMessage != null)
            {
                TMessage msg;

                using (var memoryStream = new MemoryStream(_encoding.GetBytes(textMessage.Text)))
                {
                    msg = deserializer.Deserialize(memoryStream);
                }

                return msg;
            }

            return base.DeserializeMessage(message, deserializer);
        }
    }
}