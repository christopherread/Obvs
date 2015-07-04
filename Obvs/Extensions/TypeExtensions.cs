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

        internal static Tuple<MethodInfo, Type, Type>[] GetSubscriberMethods<TCommand, TEvent, TRequest, TResponse>(this Type subscriberType)
        {
            var publicMethods = subscriberType.GetMethods(BindingFlags.Instance | BindingFlags.Public);

            // get message handler methods
            var methods = publicMethods
                .Where(m => m.ReturnType == typeof (void))
                .Where(m =>
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 1) return false;
                    var parameterInfo = parameters[0];
                    var parameterType = parameterInfo.ParameterType;

                    return (parameterType.IsInterface || parameterType.IsClass) && 
                           !parameterInfo.IsOptional &&
                           (typeof (TCommand).IsAssignableFrom(parameterType) || typeof (TEvent).IsAssignableFrom(parameterType));
                })
                .Select(m => new Tuple<MethodInfo, Type, Type>(m, m.GetParameters()[0].ParameterType, typeof(void)))
                .ToArray();

            // get request/response functions
            var functions = publicMethods
                .Where(m => m.ReturnType == typeof(IObservable<TResponse>))
                .Where(m =>
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 1) return false;
                    var parameterInfo = parameters[0];
                    var parameterType = parameterInfo.ParameterType;

                    return (parameterType.IsInterface || parameterType.IsClass) && 
                           !parameterInfo.IsOptional && 
                           typeof(TRequest).IsAssignableFrom(parameterType);
                })
                .Select(m => new Tuple<MethodInfo, Type, Type>(m, m.GetParameters()[0].ParameterType, typeof(IObservable<TResponse>)))
                .ToArray();

            // filter out methods where message is a subtype of one that is already handled
            var subscriberMethods = methods.Concat(functions)
                .Where(methodHandler => !methods.Any(mh => mh.Item1 != methodHandler.Item1 && 
                                                           mh.Item2.IsAssignableFrom(methodHandler.Item2)))
                .ToArray();

            return subscriberMethods;
        }
    }
}