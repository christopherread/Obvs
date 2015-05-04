using System;
using System.Reactive.Concurrency;
using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using Obvs.MessageProperties;
using Obvs.Serialization;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Configuration
{
    public static class DestinationFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(Lazy<IConnection> lazyConnection, string destination, DestinationType destinationType, IMessageSerializer messageSerializer, IScheduler scheduler,
                                                                           IMessagePropertyProvider<TMessage> propertyProvider = null, Func<TMessage, MsgDeliveryMode> deliveryMode = null, Func<TMessage, MsgPriority> priority = null, Func<TMessage, TimeSpan> timeToLive = null)
            where TMessage : IMessage
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

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(Lazy<IConnection> lazyConnection, string destination, DestinationType destinationType, IMessageDeserializerFactory deserializerFactory, string assemblyNameContains = null, string selector = null)
            where TServiceMessage : IMessage
            where TMessage : IMessage
        {
            return new MessageSource<TMessage>(
                lazyConnection,
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyNameContains),
                CreateDestination(destination, destinationType),
                AcknowledgementMode.AutoAcknowledge,
                selector);
        }

        private static IDestination CreateDestination(string name, DestinationType type)
        {
            return type == DestinationType.Queue ? (IDestination)new ActiveMQQueue(name) : new ActiveMQTopic(name);
        }
    }
}