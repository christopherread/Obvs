using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using Apache.NMS;
using Obvs.Serialization;

namespace Obvs.ActiveMQ
{
    /// <summary>
    /// Sends messages to an ActiveMQ queue or topic as text messages, for a serializer that writes UTF-8 text, such as
    /// JSON. Otherwise as <see cref="MessagePublisher{TMessage}"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class TextMessagePublisher<TMessage> : MessagePublisher<TMessage> where TMessage : class
    {
        /// <summary>Creates a publisher to <paramref name="destination"/> that sends messages one at a time, in order, on a scheduler of its own.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first message</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="propertyProvider">The properties to send with each message, which sources can filter or select on. None when null</param>
        /// <param name="deliveryMode">Whether each message is persistent. Non-persistent when null</param>
        /// <param name="priority">Each message's priority. Normal when null</param>
        /// <param name="timeToLive">How long each message lives. For ever when null</param>
        public TextMessagePublisher(Lazy<IConnection> lazyConnection, 
            IDestination destination, IMessageSerializer serializer, 
            Func<TMessage, Dictionary<string, object>> propertyProvider, 
            Func<TMessage, MsgDeliveryMode> deliveryMode = null,
            Func<TMessage, MsgPriority> priority = null, 
            Func<TMessage, TimeSpan> timeToLive = null)
            : base(lazyConnection, destination, serializer, propertyProvider, deliveryMode, priority, timeToLive)
        {
        }

        /// <summary>Creates a publisher to <paramref name="destination"/> that sends messages on <paramref name="scheduler"/>.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first message</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="propertyProvider">The properties to send with each message, which sources can filter or select on. None when null</param>
        /// <param name="scheduler">Where messages are sent, which must run one at a time</param>
        /// <param name="deliveryMode">Whether each message is persistent. Non-persistent when null</param>
        /// <param name="priority">Each message's priority. Normal when null</param>
        /// <param name="timeToLive">How long each message lives. For ever when null</param>
        public TextMessagePublisher(Lazy<IConnection> lazyConnection, 
            IDestination destination,
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, object>> propertyProvider,
            IScheduler scheduler, 
            Func<TMessage, MsgDeliveryMode> deliveryMode = null, 
            Func<TMessage, MsgPriority> priority = null, 
            Func<TMessage, TimeSpan> timeToLive = null)
            : base(lazyConnection, destination, serializer, propertyProvider, scheduler, deliveryMode, priority, timeToLive)
        {
        }

        /// <summary>Creates a publisher to <paramref name="destination"/> that sends messages on <paramref name="taskScheduler"/>.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first message</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="propertyProvider">The properties to send with each message, which sources can filter or select on. None when null</param>
        /// <param name="taskScheduler">Where messages are sent, which must run one at a time, for example a <see cref="ConcurrentExclusiveSchedulerPair"/>'s exclusive scheduler</param>
        /// <param name="deliveryMode">Whether each message is persistent. Non-persistent when null</param>
        /// <param name="priority">Each message's priority. Normal when null</param>
        /// <param name="timeToLive">How long each message lives. For ever when null</param>
        public TextMessagePublisher(Lazy<IConnection> lazyConnection, 
            IDestination destination, 
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, object>> propertyProvider,
            TaskScheduler taskScheduler, 
            Func<TMessage, MsgDeliveryMode> deliveryMode = null, 
            Func<TMessage, MsgPriority> priority = null, 
            Func<TMessage, TimeSpan> timeToLive = null)
            : base(lazyConnection, destination, serializer, propertyProvider, taskScheduler, deliveryMode, priority, timeToLive)
        {
        }

        /// <inheritdoc />
        protected override IMessage GenerateMessage(TMessage message, IMessageProducer producer, IMessageSerializer serializer)
        {
            var textMessage = producer.CreateTextMessage();

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, message);
                textMessage.Text = Encoding.UTF8.GetString(stream.ToArray()); // Assume the serializer does UTF8 text...
            }

            return textMessage;
        }
    }
}