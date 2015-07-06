using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Obvs.Extensions;

namespace Obvs.Configuration
{
    public static class MessageTypes
    {
        public static IEnumerable<Type> Get<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            assemblyFilter = assemblyFilter ?? (assembly => true);
            typeFilter = typeFilter ?? (type => true);

            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(assemblyFilter)
                .SelectMany(GetTypes)
                .Where(typeFilter)
                .Where(t => t.IsValidMessageType<TMessage, TServiceMessage>())
                .ToArray();

            EnsureTypesAreVisible(types);

            EnsureDefaultConstructors(types);

            return types.ToArray();
        }

        private static void EnsureDefaultConstructors(Type[] types)
        {
            var noDefaultConstructor = types.Where(t => !t.HasDefaultConstructor()).ToArray();

            if (noDefaultConstructor.Any())
            {
                throw new Exception(
                    "The following message types do not have a default constructors and may not deserialize. Please add a default constuctor: " + Environment.NewLine +
                    string.Join(Environment.NewLine, noDefaultConstructor.Select(t => string.Format("- {0}", t.FullName))));
            }
        }

        private static void EnsureTypesAreVisible(IEnumerable<Type> types)
        {
            var notVisible = types.Where(t => !t.IsVisible).ToArray();

            if (notVisible.Any())
            {
                throw new Exception(
                    "The following message types are not visible so Obvs will not be able to deserialize them. Please mark as public: " + Environment.NewLine +
                    string.Join(Environment.NewLine, notVisible.Select(t => string.Format("- {0}", t.FullName))));
            }
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