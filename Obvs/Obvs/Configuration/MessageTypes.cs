using System;
using System.Collections.Generic;
using System.Linq;
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
                .Where(assembly => string.IsNullOrEmpty(assemblyNameContains) || assembly.FullName.Contains(assemblyNameContains))
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(TMessage).IsAssignableFrom(type) &&
                               typeof(TServiceMessage).IsAssignableFrom(type) &&
                               type.IsClass &&
                               type.IsVisible);
        }
    }
}