using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Obvs.Hosting;
using Obvs.MessageProperties;
using Obvs.Serialization;

namespace Obvs.AzureSB
{
    internal sealed class MessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private readonly ServiceBusSender _sender;
        private readonly IMessageSerializer _serializer;
        private readonly string _replyTo;

        /// <param name="replyTo">The queue responses should go to, for requests</param>
        public MessagePublisher(ServiceBusSender sender, IMessageSerializer serializer, string replyTo = null)
        {
            _sender = sender;
            _serializer = serializer;
            _replyTo = replyTo;
        }

        public Task PublishAsync(TMessage message)
        {
            var serviceBusMessage = ToServiceBusMessage(message, _serializer);
            serviceBusMessage.ReplyTo = _replyTo;
            return _sender.SendMessageAsync(serviceBusMessage);
        }

        // The type name tells the receiver which deserializer to use. Obvs headers (such as trace context) become
        // application properties, apart from the type name.
        public static ServiceBusMessage ToServiceBusMessage(object message, IMessageSerializer serializer)
        {
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, message);
                var serviceBusMessage = new ServiceBusMessage(stream.ToArray());
                foreach (var header in ObvsMessageHeaders.Get(message))
                {
                    if (header.Key != MessagePropertyNames.TypeName)
                    {
                        serviceBusMessage.ApplicationProperties[header.Key] = header.Value;
                    }
                }
                serviceBusMessage.ApplicationProperties[MessagePropertyNames.TypeName] = message.GetType().Name;
                return serviceBusMessage;
            }
        }

        public void Dispose()
        {
            _sender.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
