using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Obvs.Extensions
{
    public static class TypeExtensions
    {
        public static string GetSimpleName(this TypeInfo t)
        {
            if (t.IsGenericType)
            {
                string name = t.Name;
                int index = name.IndexOf('`');
                return index == -1 ? name : name.Substring(0, index);
            }
            return t.Name;
        }

        public static bool HasDefaultConstructor(this TypeInfo type)
        {
            return (type.GetConstructor(Type.EmptyTypes) != null || !type.GetConstructors().Any());
        }

        public static bool IsValidMessageType<TMessage, TServiceMessage>(this TypeInfo type)
            where TMessage : class
            where TServiceMessage : class
        {
            return typeof(TMessage).GetTypeInfo().IsAssignableFrom(type) &&
                   typeof(TServiceMessage).GetTypeInfo().IsAssignableFrom(type) &&
                   type.IsClass;
        }

        internal static Tuple<MethodInfo, TypeInfo, TypeInfo>[] GetSubscriberMethods<TCommand, TEvent, TRequest, TResponse>(this Type subscriberType)
        {
            var publicMethods = subscriberType.GetTypeInfo().GetMethods(BindingFlags.Instance | BindingFlags.Public);

            // get message handler methods
            var methods = publicMethods
                .Where(m => m.ReturnType == typeof (void))
                .Where(m =>
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 1) return false;
                    var parameterInfo = parameters[0];
                    var parameterType = parameterInfo.ParameterType.GetTypeInfo();

                    return (parameterType.IsInterface || parameterType.IsClass) && 
                           !parameterInfo.IsOptional &&
                           (typeof (TCommand).GetTypeInfo().IsAssignableFrom(parameterType) || typeof (TEvent).GetTypeInfo().IsAssignableFrom(parameterType));
                })
                .Select(m => new Tuple<MethodInfo, TypeInfo, TypeInfo>(m, m.GetParameters()[0].ParameterType.GetTypeInfo(), typeof(void).GetTypeInfo()))
                .ToArray();

            // get request/response functions
            var functions = publicMethods
                .Where(m => m.ReturnType == typeof(IObservable<TResponse>))
                .Where(m =>
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 1) return false;
                    var parameterInfo = parameters[0];
                    var parameterType = parameterInfo.ParameterType.GetTypeInfo();

                    return (parameterType.IsInterface || parameterType.IsClass) && 
                           !parameterInfo.IsOptional && 
                           typeof(TRequest).GetTypeInfo().IsAssignableFrom(parameterType);
                })
                .Select(m => new Tuple<MethodInfo, TypeInfo, TypeInfo>(m, m.GetParameters()[0].ParameterType.GetTypeInfo(), typeof(IObservable<TResponse>).GetTypeInfo()))
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