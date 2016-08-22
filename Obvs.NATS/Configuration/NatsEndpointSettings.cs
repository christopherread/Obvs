using System;
using System.Collections.Generic;

namespace Obvs.NATS.Configuration
{
    public class NatsEndpointSettings<TMessage>
        where TMessage : class
    {
        public string ServiceName { get; set; }
        public MessageProperty MessageProperties { get; private set; }
        public BrokerConnection Connection { get; private set; }

        public class BrokerConnection
        {
            public string Url { get; set; }
            public bool IsShared { get; set; }
        }

        public class MessageProperty
        {
            public Func<IDictionary<string, string>, bool> Filter { get; set; }
            public Func<TMessage, Dictionary<string, string>> Provider { get; set; }
        }

        public void Configure(Action<MessageProperty> configure)
        {
            MessageProperties = new MessageProperty();
            configure(MessageProperties);
        }

        public void Configure(Action<BrokerConnection> configure)
        {
            Connection = new BrokerConnection();
            configure(Connection);
        }
    }
}