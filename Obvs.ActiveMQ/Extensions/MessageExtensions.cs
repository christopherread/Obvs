using System.Collections.Generic;
using Apache.NMS;

namespace Obvs.ActiveMQ.Extensions
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
                else if (keyValuePair.Value is short)
                {
                    message.Properties.SetShort(keyValuePair.Key, (short)keyValuePair.Value);
                }
                else if (keyValuePair.Value is float)
                {
                    message.Properties.SetFloat(keyValuePair.Key, (float)keyValuePair.Value);
                }
                else if (keyValuePair.Value is char)
                {
                    message.Properties.SetChar(keyValuePair.Key, (char)keyValuePair.Value);
                }
                else if (keyValuePair.Value is byte)
                {
                    message.Properties.SetByte(keyValuePair.Key, (byte)keyValuePair.Value);
                }
                else if (keyValuePair.Value is byte[])
                {
                    message.Properties.SetBytes(keyValuePair.Key, (byte[])keyValuePair.Value);
                }
            }

            return message;
        }

        public static void Send(this IMessage message, IMessageProducer producer)
        {
            producer.Send(message);
        }
    }
}