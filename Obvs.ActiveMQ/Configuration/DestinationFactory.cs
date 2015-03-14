using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Configuration
{
    public static class DestinationFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(string broker, string destination, string serviceName, DestinationType destinationType, IMessageSerializer messageSerializer, IMessagePropertyProvider<TMessage> propertyProvider = null)
            where TMessage : IMessage
        {
            return new MessagePublisher<TMessage>(
                new ConnectionFactory(broker, ConnectionClientId.CreateWithSuffix(string.Format("{0}.{1}.Publisher", serviceName, typeof(TMessage).Name))),
                CreateDestination(destination, destinationType),
                messageSerializer,
                propertyProvider ?? new DefaultPropertyProvider<TMessage>());
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(string broker, string destination, string serviceName, DestinationType destinationType, IMessageDeserializerFactory deserializerFactory, string assemblyNameContains = null, string selector = null)
            where TServiceMessage : IMessage
            where TMessage : IMessage
        {
            return new MessageSource<TMessage>(
                new ConnectionFactory(broker, ConnectionClientId.CreateWithSuffix(string.Format("{0}.{1}.Source", serviceName, typeof(TMessage).Name))),
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