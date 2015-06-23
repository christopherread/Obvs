using System;
using System.Linq;

namespace Obvs.Extensions
{
    public static class TypeExtensions
    {
        public static string GetSimpleName(this Type t)
        {
            if (t.IsGenericType)
            {
                string name = t.Name;
                int index = name.IndexOf('`');
                return index == -1 ? name : name.Substring(0, index);
            }
            return t.Name;
        }

        public static bool HasDefaultConstructor(this Type type)
        {
            return (type.GetConstructor(Type.EmptyTypes) != null || !type.GetConstructors().Any());
        }

        public static bool IsValidMessageType<TMessage, TServiceMessage>(this Type type)
            where TMessage : class
            where TServiceMessage : class
        {
            return typeof(TMessage).IsAssignableFrom(type) &&
                   typeof(TServiceMessage).IsAssignableFrom(type) &&
                   type.IsClass;
        }
    }
}