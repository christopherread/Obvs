using System;
using System.Collections.Generic;

namespace Obvs.NATS.Configuration
{
    public class NatsEndpointSettings<TMessage>
        where TMessage : class
    {
        public string BrokerUri { get; set; }
        public string ServiceName { get; set; }
        public MessageProperty MessageProperties { get; private set; }

        public class MessageProperty
        {
            public Func<IDictionary<string, string>, bool> Filter { get; set; }
            public Func<TMessage, Dictionary<string, string>> Provider { get; set; }
        }

        public void Configure(Action<MessageProperty> configureMessageProperties)
        {
            MessageProperties = new MessageProperty();
            configureMessageProperties(MessageProperties);
        }
    }
}