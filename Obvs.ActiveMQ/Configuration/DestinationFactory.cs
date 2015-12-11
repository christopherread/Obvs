using System;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.ActiveMQ.Configuration
{
    public static class DestinationFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(
            Lazy<IConnection> lazyConnection, 
            string destination, 
            DestinationType destinationType,
            IMessageSerializer messageSerializer,
            TaskScheduler scheduler = null,
            IMessagePropertyProvider<TMessage> propertyProvider = null, 
            Func<TMessage, MsgDeliveryMode> deliveryMode = null, 
            Func<TMessage, MsgPriority> priority = null, 
            Func<TMessage, TimeSpan> timeToLive = null) 
            where TMessage : class
        {
            return new MessagePublisher<TMessage>(
                lazyConnection,
                CreateDestination(destination, destinationType),
                messageSerializer,
                propertyProvider ?? new DefaultPropertyProvider<TMessage>(),
                scheduler,
                deliveryMode, 
                priority, 
                timeToLive);
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(Lazy<IConnection> lazyConnection, string destination, DestinationType destinationType, IMessageDeserializerFactory deserializerFactory, Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null, string selector = null, AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge)
            where TMessage : class
            where TServiceMessage : class
        {
            return new MessageSource<TMessage>(
                lazyConnection,
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyFilter, typeFilter),
                CreateDestination(destination, destinationType),
                mode,
                selector);
        }

        private static IDestination CreateDestination(string name, DestinationType type)
        {
            return type == DestinationType.Queue ? (IDestination)new ActiveMQQueue(name) : new ActiveMQTopic(name);
        }
    }
}