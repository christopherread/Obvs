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
    public class TextMessagePublisher<TMessage> : MessagePublisher<TMessage> where TMessage : class
    {
        public TextMessagePublisher(Lazy<IConnection> lazyConnection, 
            IDestination destination, IMessageSerializer serializer, 
            Func<TMessage, Dictionary<string, object>> propertyProvider, 
            Func<TMessage, MsgDeliveryMode> deliveryMode = null,
            Func<TMessage, MsgPriority> priority = null, 
            Func<TMessage, TimeSpan> timeToLive = null)
            : base(lazyConnection, destination, serializer, propertyProvider, deliveryMode, priority, timeToLive)
        {
        }

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