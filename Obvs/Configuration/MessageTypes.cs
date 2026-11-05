using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Obvs.Extensions;

namespace Obvs.Configuration
{
    /// <summary>Finds message types by scanning the app's assemblies.</summary>
    public static class MessageTypes
    {
        /// <summary>
        /// This is inspired by http://www.michael-whelan.net/replacing-appdomain-in-dotnet-core/
        /// 
        /// More specifically:
        /// https://github.com/mwhelan/Specify/blob/master/src/app/Specify/lib/AssemblyTypeResolver.cs
        /// </summary>
        static class AppDomainReplacement
        {
            /// <summary>
            /// Gets all assemblies from the AppDomain.
            /// </summary>
            /// <returns>IEnumerable&lt;Assembly&gt;.</returns>
            public static IEnumerable<Assembly> GetAllAssembliesFromAppDomain()
            {
#if NETFRAMEWORK
                return AppDomain.CurrentDomain.GetAssemblies();
#else
                // A single-file app has no dependency context, which AssembliesThatUseObvs handles
#pragma warning disable IL3002
                return AssembliesThatUseObvs(Microsoft.Extensions.DependencyModel.DependencyContext.Default, Assembly.GetEntryAssembly());
#pragma warning restore IL3002
#endif
            }
        }

#if !NETFRAMEWORK
        // Obvs and the assemblies that reference an Obvs assembly, which hold the app's messages. They're the libraries in
        // the app's dependency context or, in a single-file app, which has none, those the entry assembly references.
        internal static IReadOnlyList<Assembly> AssembliesThatUseObvs(Microsoft.Extensions.DependencyModel.DependencyContext dependencies, Assembly entryAssembly)
        {
            var assemblies = new List<Assembly> { typeof(MessageTypes).Assembly };
            if (dependencies != null)
            {
                foreach (var library in dependencies.RuntimeLibraries)
                {
                    if (library.Dependencies.Any(dependency => UsesObvs(dependency.Name)))
                    {
                        assemblies.Add(Assembly.Load(new AssemblyName(library.Name)));
                    }
                }
                return assemblies;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { typeof(MessageTypes).Assembly.GetName().Name };
            var pending = new Queue<Assembly>();
            if (entryAssembly != null && seen.Add(entryAssembly.GetName().Name))
            {
                pending.Enqueue(entryAssembly);
            }
            while (pending.Count > 0)
            {
                var assembly = pending.Dequeue();
                var references = assembly.GetReferencedAssemblies();
                if (references.Any(reference => UsesObvs(reference.Name)))
                {
                    assemblies.Add(assembly);
                }
                // The framework's assemblies don't reference the app's, so there's nothing to find below them
                foreach (var reference in references.Where(reference => !IsFramework(reference.Name) && seen.Add(reference.Name)))
                {
                    try
                    {
                        pending.Enqueue(Assembly.Load(reference));
                    }
                    catch (Exception exception) when (exception is System.IO.FileNotFoundException || exception is System.IO.FileLoadException || exception is BadImageFormatException)
                    {
                        // A reference the app doesn't ship, and so never loads, holds none of its messages
                    }
                }
            }
            return assemblies;
        }

        private static bool UsesObvs(string referenceName)
        {
            return referenceName.StartsWith(nameof(Obvs), StringComparison.Ordinal);
        }

        private static bool IsFramework(string assemblyName)
        {
            return assemblyName == "System" || assemblyName == "mscorlib" || assemblyName == "netstandard" ||
                   assemblyName.StartsWith("System.", StringComparison.Ordinal);
        }
#endif

        /// <summary>
        /// The public, concrete types that are <typeparamref name="TMessage"/> and <typeparamref name="TServiceMessage"/>, in the
        /// assemblies that reference Obvs, filtered by <paramref name="assemblyFilter"/> and <paramref name="typeFilter"/>.
        /// </summary>
        /// <param name="assemblyFilter">Which assemblies to scan, or all</param>
        /// <param name="typeFilter">Which types to include, or all</param>
        public static IEnumerable<Type> Get<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
            where TMessage : class
            where TServiceMessage : class
        {
            assemblyFilter = assemblyFilter ?? (assembly => true);
            typeFilter = typeFilter ?? (type => true);

            var types = AppDomainReplacement
                .GetAllAssembliesFromAppDomain()
                .Where(assemblyFilter)
                .SelectMany(GetTypes)
                .Where(typeFilter)
                .Where(t => t.IsValidMessageType<TMessage, TServiceMessage>())
                .ToArray();

            EnsureTypesAreVisible(types);

            return types.ToArray();
        }

        private static void EnsureTypesAreVisible(IEnumerable<Type> types)
        {
            var notVisible = types.Where(t => !t.GetTypeInfo().IsVisible).ToArray();

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
                return assembly.GetExportedTypes();
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