using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using Obvs.Serialization;
using IMessage = Obvs.Types.IMessage;

namespace Obvs.ActiveMQ.Configuration
{
    public static class DestinationFactory
    {
        public static MessagePublisher<TMessage> CreatePublisher<TMessage>(string broker, string destination, string serviceName, IMessagePropertyProvider<TMessage> propertyProvider = null)
            where TMessage : IMessage
        {
            return new MessagePublisher<TMessage>(
                new ConnectionFactory(broker, ConnectionClientId.CreateWithSuffix(string.Format("{0}.{1}.Publisher", serviceName, typeof(TMessage).Name))),
                new ActiveMQTopic(destination),
                new JsonMessageSerializer(),
                propertyProvider ?? new DefaultPropertyProvider<TMessage>());
        }

        public static MessageSource<TMessage> CreateSource<TMessage, TServiceMessage>(string broker, string destination, string serviceName, string assemblyNameContains = null, string selector = null)
            where TServiceMessage : IMessage
            where TMessage : IMessage
        {
            return new MessageSource<TMessage>(
                new ConnectionFactory(broker, ConnectionClientId.CreateWithSuffix(string.Format("{0}.{1}.Source", serviceName, typeof(TMessage).Name))),
                DeserializerTypeFactory.CreateJson<TMessage, TServiceMessage>(assemblyNameContains),
                new ActiveMQTopic(destination),
                AcknowledgementMode.AutoAcknowledge,
                selector);
        }
    }
}