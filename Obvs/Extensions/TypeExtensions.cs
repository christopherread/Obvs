using System;
using System.Linq;

namespace Obvs.Extensions
{
    /// <summary>Helpers for message types.</summary>
    public static class TypeExtensions
    {
        /// <summary>The type's name, without a generic type's arity.</summary>
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

        /// <summary>Whether the type can be created without arguments.</summary>
        public static bool HasDefaultConstructor(this Type type)
        {
            return (type.GetConstructor(Type.EmptyTypes) != null || !type.GetConstructors().Any());
        }

        /// <summary>Whether the type is a concrete <typeparamref name="TMessage"/> and <typeparamref name="TServiceMessage"/>, which can be deserialized.</summary>
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