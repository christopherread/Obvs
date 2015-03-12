using System.Collections.Generic;
using Apache.NMS;

namespace Obvs.ActiveMQ
{
    public static class MessageExtensions
    {
        public static IMessage SetProperties(this IMessage message,
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            foreach (KeyValuePair<string, object> keyValuePair in properties)
            {
                if (keyValuePair.Value is long)
                {
                    message.Properties.SetLong(keyValuePair.Key, (long)keyValuePair.Value);
                }
                else if (keyValuePair.Value is int)
                {
                    message.Properties.SetInt(keyValuePair.Key, (int)keyValuePair.Value);
                }
                else if (keyValuePair.Value is string)
                {
                    message.Properties.SetString(keyValuePair.Key, (string)keyValuePair.Value);
                }
                else if (keyValuePair.Value is bool)
                {
                    message.Properties.SetBool(keyValuePair.Key, (bool)keyValuePair.Value);
                }
                else if (keyValuePair.Value is double)
                {
                    message.Properties.SetDouble(keyValuePair.Key, (double)keyValuePair.Value);
                }
            }

            return message;
        }

        public static void Send(this Apache.NMS.IMessage message, IMessageProducer producer)
        {
            producer.Send(message);
        }
    }
}