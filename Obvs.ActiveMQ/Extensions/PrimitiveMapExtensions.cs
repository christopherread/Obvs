using System.Collections;
using System.Collections.Generic;
using Apache.NMS;
using Obvs.MessageProperties;

namespace Obvs.ActiveMQ.Extensions
{
    internal static class PrimitiveMapExtensions
    {
        public static void AddProperties(this IPrimitiveMap primitiveMap, IEnumerable<KeyValuePair<string, object>> properties)
        {
            foreach (var keyValuePair in properties)
            {
                var str = keyValuePair.Value as string;

                if (str != null)
                {
                    primitiveMap.SetString(keyValuePair.Key, str);
                    continue;
                }

                if (keyValuePair.Value is int)
                {
                    primitiveMap.SetInt(keyValuePair.Key, (int) keyValuePair.Value);
                }
                else if (keyValuePair.Value is long)
                {
                    primitiveMap.SetLong(keyValuePair.Key, (long) keyValuePair.Value);
                }
                else if (keyValuePair.Value is bool)
                {
                    primitiveMap.SetBool(keyValuePair.Key, (bool) keyValuePair.Value);
                }
                else if (keyValuePair.Value is double)
                {
                    primitiveMap.SetDouble(keyValuePair.Key, (double) keyValuePair.Value);
                }
                else if (keyValuePair.Value is short)
                {
                    primitiveMap.SetShort(keyValuePair.Key, (short) keyValuePair.Value);
                }
                else if (keyValuePair.Value is float)
                {
                    primitiveMap.SetFloat(keyValuePair.Key, (float) keyValuePair.Value);
                }
                else if (keyValuePair.Value is char)
                {
                    primitiveMap.SetChar(keyValuePair.Key, (char) keyValuePair.Value);
                }
                else if (keyValuePair.Value is byte)
                {
                    primitiveMap.SetByte(keyValuePair.Key, (byte) keyValuePair.Value);
                }
                else if (keyValuePair.Value is byte[])
                {
                    primitiveMap.SetBytes(keyValuePair.Key, (byte[]) keyValuePair.Value);
                }
                else if (keyValuePair.Value is IDictionary)
                {
                    primitiveMap.SetDictionary(keyValuePair.Key, (IDictionary) keyValuePair.Value);
                }
                else if (keyValuePair.Value is IList)
                {
                    primitiveMap.SetList(keyValuePair.Key, (IList) keyValuePair.Value);
                }
            }
        }

        public static bool TryGetTypeName(this IPrimitiveMap properties, out string typeName)
        {
            typeName = properties.GetString(MessagePropertyNames.TypeName);
            return typeName != null;
        }
    }
}