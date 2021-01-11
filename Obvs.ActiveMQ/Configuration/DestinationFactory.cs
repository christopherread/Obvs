using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using Obvs.ActiveMQ.Utils;
using Obvs.Serialization;

namespace Obvs.ActiveMQ.Configuration
{
    // Provides a task scheduler that ensures a maximum concurrency level while 
    // running on top of the thread pool.
    public static class DestinationFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(
            Lazy<IConnection> lazyConnection, 
            string destination, 
            DestinationType destinationType, 
            IMessageSerializer messageSerializer, 
            Func<TMessage, Dictionary<string, object>> propertyProvider, 
            TaskScheduler scheduler = null, 
            Func<TMessage, MsgDeliveryMode> deliveryMode = null, 
            Func<TMessage, MsgPriority> priority = null, 
            Func<TMessage, TimeSpan> timeToLive = null) 
            where TMessage : class
        {
            return new MessagePublisher<TMessage>(
                lazyConnection,
                CreateDestination(destination, destinationType),
                messageSerializer,
                propertyProvider,
                scheduler ?? new LimitedConcurrencyLevelTaskScheduler(1),
                deliveryMode, 
                priority, 
                timeToLive);
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(
            Lazy<IConnection> lazyConnection,
            string destination,
            DestinationType destinationType,
            IMessageDeserializerFactory deserializerFactory,
            Func<IDictionary, bool> propertyFilter,
            Func<Assembly, bool> assemblyFilter = null,
            Func<Type, bool> typeFilter = null,
            string selector = null,
            AcknowledgementMode mode = AcknowledgementMode.AutoAcknowledge,
            bool noLocal = false)
            where TMessage : class
            where TServiceMessage : class
        {
            return new MessageSource<TMessage>(
                lazyConnection,
                deserializerFactory.Create<TMessage, TServiceMessage>(assemblyFilter, typeFilter),
                CreateDestination(destination, destinationType),
                mode, selector, propertyFilter, noLocal);
        }

        private static IDestination CreateDestination(string name, DestinationType type)
        {
            return type == DestinationType.Queue ? (IDestination)new ActiveMQQueue(name) : new ActiveMQTopic(name);
        }
    }
}