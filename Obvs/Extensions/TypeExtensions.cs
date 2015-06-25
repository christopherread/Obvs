using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

        internal static KeyValuePair<MethodInfo, Type>[] GetSubscriberMethods<TCommand, TEvent>(this Type subscriberType)
        {
            var methods = subscriberType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.ReturnType == typeof (void))
                .Where(m =>
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 1) return false;
                    var parameterInfo = parameters[0];
                    var parameterType = parameterInfo.ParameterType;

                    return parameterType.IsClass && !parameterInfo.IsOptional &&
                           (typeof (TCommand).IsAssignableFrom(parameterType) ||
                            typeof (TEvent).IsAssignableFrom(parameterType));
                })
                .Select(m => new KeyValuePair<MethodInfo, Type>(m, m.GetParameters()[0].ParameterType))
                .ToArray();

            return methods.Where(methodHandler => !methods.Any(mh => mh.Key != methodHandler.Key && mh.Value.IsAssignableFrom(methodHandler.Value))).ToArray();
        }
    }
}