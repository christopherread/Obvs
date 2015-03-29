using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Obvs.Types;

namespace Obvs.Configuration
{
    public static class MessageTypes
    {
        public static IEnumerable<Type> Get<TMessage, TServiceMessage>(string assemblyNameContains = null)
            where TMessage : IMessage
            where TServiceMessage : IMessage
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(assembly => MatchesFilter(assemblyNameContains, assembly))
                .SelectMany(GetTypes)
                .Where(IsValidMessageType<TMessage, TServiceMessage>)
                .ToArray();
        }

        private static bool MatchesFilter(string assemblyNameContains, Assembly assembly)
        {
            return string.IsNullOrEmpty(assemblyNameContains) || assembly.FullName.Contains(assemblyNameContains);
        }

        private static bool IsValidMessageType<TMessage, TServiceMessage>(Type type) 
            where TMessage : IMessage 
            where TServiceMessage : IMessage
        {
            return typeof(TMessage).IsAssignableFrom(type) &&
                   typeof(TServiceMessage).IsAssignableFrom(type) &&
                   type.IsClass &&
                   type.IsVisible;
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (Exception exception)
            {
                throw new Exception(string.Format(
                        "Error loading types from assembly '{0}', please consider using the provided configuration options to filter which assemblies you would like to load your message types from.",
                        assembly.FullName), exception);
            }
        }
    }
}