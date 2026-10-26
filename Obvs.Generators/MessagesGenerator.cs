using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Obvs.Generators
{
    // Writes the Types property of each partial class marked [ObvsMessages(typeof(IServiceMessage))]: every concrete
    // class in the same assembly implementing that interface, for the service's AddMessages(...)
    [Generator(LanguageNames.CSharp)]
    public sealed class MessagesGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor NotPartial = new DiagnosticDescriptor(
            "OBVS100",
            "An [ObvsMessages] class must be partial",
            "'{0}' must be partial, so Obvs can add its Types property. Add the partial modifier to its declaration.",
            "Obvs",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor Nested = new DiagnosticDescriptor(
            "OBVS101",
            "An [ObvsMessages] class can't be nested",
            "'{0}' is nested inside '{1}'. Declare it directly in a namespace, so Obvs can add its Types property.",
            "Obvs",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var registries = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Obvs.Types.ObvsMessagesAttribute",
                (node, _) => node is ClassDeclarationSyntax,
                (attributed, _) => Registry.From(attributed));

            context.RegisterSourceOutput(registries, (output, registry) =>
            {
                if (registry.Problem != null)
                {
                    output.ReportDiagnostic(registry.Problem);
                    return;
                }
                output.AddSource($"{registry.MetadataName}.ObvsMessages.g.cs", SourceText.From(registry.Source(), Encoding.UTF8));
            });
        }

        private sealed class Registry
        {
            private Registry(string metadataName, string? @namespace, string name, ImmutableArray<string> types, Diagnostic? problem)
            {
                MetadataName = metadataName;
                Namespace = @namespace;
                Name = name;
                Types = types;
                Problem = problem;
            }

            public string MetadataName { get; }
            public string? Namespace { get; }
            public string Name { get; }
            public ImmutableArray<string> Types { get; }
            public Diagnostic? Problem { get; }

            public static Registry From(GeneratorAttributeSyntaxContext attributed)
            {
                var registry = (INamedTypeSymbol)attributed.TargetSymbol;
                var declaration = (ClassDeclarationSyntax)attributed.TargetNode;
                var metadataName = registry.ToDisplayString().Replace('<', '_').Replace('>', '_');
                var @namespace = registry.ContainingNamespace.IsGlobalNamespace ? null : registry.ContainingNamespace.ToDisplayString();

                Diagnostic? problem = null;
                if (registry.ContainingType != null)
                {
                    problem = Diagnostic.Create(Nested, declaration.Identifier.GetLocation(), registry.Name, registry.ContainingType.Name);
                }
                else if (!registry.DeclaringSyntaxReferences.Any(reference =>
                             reference.GetSyntax() is ClassDeclarationSyntax part && part.Modifiers.Any(SyntaxKind.PartialKeyword)))
                {
                    problem = Diagnostic.Create(NotPartial, declaration.Identifier.GetLocation(), registry.Name);
                }

                var serviceMessage = attributed.Attributes[0].ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                var types = problem != null || serviceMessage == null
                    ? ImmutableArray<string>.Empty
                    : MessageTypes(attributed.SemanticModel.Compilation, registry, serviceMessage);

                return new Registry(metadataName, @namespace, registry.Name, types, problem);
            }

            // Abstract classes and interfaces are left out as bases. Everything else is listed, even classes the bus
            // will reject, so its startup checks can say what's wrong; only classes the registry can't name are skipped.
            private static ImmutableArray<string> MessageTypes(Compilation compilation, INamedTypeSymbol registry, INamedTypeSymbol serviceMessage)
            {
                return AllTypes(compilation.Assembly.GlobalNamespace)
                    .Where(type => type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsStatic && !IsGeneric(type))
                    .Where(type => type.AllInterfaces.Contains(serviceMessage, SymbolEqualityComparer.Default))
                    .Where(type => compilation.IsSymbolAccessibleWithin(type, registry))
                    .Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .OrderBy(name => name, System.StringComparer.Ordinal)
                    .ToImmutableArray();
            }

            private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol @namespace)
            {
                foreach (var type in @namespace.GetTypeMembers().SelectMany(WithNested))
                {
                    yield return type;
                }
                foreach (var type in @namespace.GetNamespaceMembers().SelectMany(AllTypes))
                {
                    yield return type;
                }
            }

            private static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type)
            {
                return new[] { type }.Concat(type.GetTypeMembers().SelectMany(WithNested));
            }

            private static bool IsGeneric(INamedTypeSymbol type)
            {
                for (var current = type; current != null; current = current.ContainingType)
                {
                    if (current.TypeParameters.Length > 0)
                    {
                        return true;
                    }
                }
                return false;
            }

            public string Source()
            {
                var indent = Namespace == null ? "" : "    ";
                var source = new StringBuilder().AppendLine("// <auto-generated/>");
                if (Namespace != null)
                {
                    source.AppendLine($"namespace {Namespace}").AppendLine("{");
                }
                source
                    .AppendLine($"{indent}partial class {Name}")
                    .AppendLine($"{indent}{{")
                    .AppendLine($"{indent}    /// <summary>The message types in this assembly, for the service's AddMessages(...).</summary>")
                    .AppendLine($"{indent}    public static global::System.Type[] Types => new global::System.Type[]")
                    .AppendLine($"{indent}    {{");
                foreach (var type in Types)
                {
                    source.AppendLine($"{indent}        typeof({type}),");
                }
                source
                    .AppendLine($"{indent}    }};")
                    .AppendLine($"{indent}}}");
                if (Namespace != null)
                {
                    source.AppendLine("}");
                }
                return source.ToString();
            }
        }
    }
}
