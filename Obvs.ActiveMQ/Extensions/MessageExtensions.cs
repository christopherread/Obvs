using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apache.NMS;
using Obvs.MessageProperties;

namespace Obvs.ActiveMQ.Extensions
{
    public static class MessageExtensions
    {
        public static IMessage SetProperties(this IMessage message,
            IEnumerable<KeyValuePair<string, object>> properties)
        {
            foreach (var keyValuePair in properties)
            {
                var str = keyValuePair.Value as string;
                if (str != null)
                {
                    message.Properties.SetString(keyValuePair.Key, str);
                    continue;
                }

                if (keyValuePair.Value is int)
                {
                    message.Properties.SetInt(keyValuePair.Key, (int)keyValuePair.Value);
                }
                else if (keyValuePair.Value is long)
                {
                    message.Properties.SetLong(keyValuePair.Key, (long) keyValuePair.Value);
                }
                else if (keyValuePair.Value is bool)
                {
                    message.Properties.SetBool(keyValuePair.Key, (bool) keyValuePair.Value);
                }
                else if (keyValuePair.Value is double)
                {
                    message.Properties.SetDouble(keyValuePair.Key, (double) keyValuePair.Value);
                }
                else if (keyValuePair.Value is short)
                {
                    message.Properties.SetShort(keyValuePair.Key, (short) keyValuePair.Value);
                }
                else if (keyValuePair.Value is float)
                {
                    message.Properties.SetFloat(keyValuePair.Key, (float) keyValuePair.Value);
                }
                else if (keyValuePair.Value is char)
                {
                    message.Properties.SetChar(keyValuePair.Key, (char) keyValuePair.Value);
                }
                else if (keyValuePair.Value is byte)
                {
                    message.Properties.SetByte(keyValuePair.Key, (byte) keyValuePair.Value);
                }
                else if (keyValuePair.Value is byte[])
                {
                    message.Properties.SetBytes(keyValuePair.Key, (byte[]) keyValuePair.Value);
                }
                else if (keyValuePair.Value is IDictionary)
                {
                    message.Properties.SetDictionary(keyValuePair.Key, (IDictionary) keyValuePair.Value);
                }
                else if (keyValuePair.Value is IList)
                {
                    message.Properties.SetList(keyValuePair.Key, (IList) keyValuePair.Value);
                }
            }

            return message;
        }

        public static IDictionary<string, string> GetProperties(this IMessage message)
        {
            return message.Properties.Keys.Cast<string>()
                .ToDictionary(key => key, key => message.Properties.GetString(key));
        }

        public static void Send(this IMessage message, IMessageProducer producer, MsgDeliveryMode deliveryMode,
            MsgPriority priority, TimeSpan timeToLive)
        {
            producer.Send(message, deliveryMode, priority, timeToLive);
        }

        public static bool TryGetTypeName(this IMessage message, out string typeName)
        {
            typeName = message.Properties.GetString(MessagePropertyNames.TypeName);
            return typeName != null;
        }
    }
}