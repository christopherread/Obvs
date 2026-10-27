using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Obvs.Analyzers
{
    /// <summary>OBVS002: a message must be exactly one of ICommand, IEvent, IRequest or IResponse.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MessageKindAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string[] Kinds = { "Obvs.Types.ICommand", "Obvs.Types.IEvent", "Obvs.Types.IRequest", "Obvs.Types.IResponse" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rules.MoreThanOneKind);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var kinds = Kinds.Select(start.Compilation.GetTypeByMetadataName).Where(kind => kind != null).Select(kind => kind!).ToImmutableArray();
                if (kinds.Length != Kinds.Length)
                {
                    return;
                }
                start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, kinds), SymbolKind.NamedType);
            });
        }

        private static void Analyze(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> kinds)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            {
                return;
            }
            var implemented = kinds.Where(kind => type.AllInterfaces.Contains(kind, SymbolEqualityComparer.Default)).ToArray();
            if (implemented.Length > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.MoreThanOneKind, type.Locations.FirstOrDefault(),
                    type.Name, string.Join(" and ", implemented.Select(kind => kind.Name))));
            }
        }
    }
}
