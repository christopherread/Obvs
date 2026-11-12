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
    /// Receives text messages, and bytes messages, from an ActiveMQ queue or topic, reading text in the encoding it's
    /// given. Otherwise as <see cref="MessageSource{TMessage}"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class TextMessageSource<TMessage> : MessageSource<TMessage> where TMessage : class
    {
        private readonly Encoding _encoding;

        /// <summary>Creates a source of the messages sent to <paramref name="destination"/>.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first subscription</param>
        /// <param name="deserializers">The deserializers, one for each type of message, chosen by the type's name the publisher sends</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="mode">When messages are acknowledged: as they're delivered, by default, or once deserialized</param>
        /// <param name="selector">An ActiveMQ message selector, which the broker filters messages with. All of them when null</param>
        /// <param name="encoding">The text's encoding. UTF-8 when null</param>
        /// <param name="propertyFilter">Which messages to receive, by their properties, once the broker has delivered them. All of them when null</param>
        /// <param name="noLocal">Whether to skip messages sent on the same connection</param>
        public TextMessageSource(Lazy<IConnection> lazyConnection, IEnumerable<IMessageDeserializer<TMessage>> deserializers,
            IDestination destination, AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge, string selector = null, 
            Encoding encoding = null, Func<IDictionary, bool> propertyFilter = null, bool noLocal = false)
            : base(lazyConnection, deserializers, destination, mode, selector, propertyFilter, noLocal)
        {
            _encoding = encoding ?? Encoding.UTF8;
        }

        /// <inheritdoc />
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