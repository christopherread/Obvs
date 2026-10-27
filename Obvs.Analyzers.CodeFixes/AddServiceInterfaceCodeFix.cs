using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace Obvs.Analyzers.CodeFixes
{
    /// <summary>OBVS001: makes the message implement the service's message interface.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class AddServiceInterfaceCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Rules.MessageOfAnotherService.Id);

        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            if (!diagnostic.Properties.TryGetValue(ServiceConfigurationAnalyzer.MessageTypeProperty, out var messageId) ||
                !diagnostic.Properties.TryGetValue(Rules.ServiceInterfaceProperty, out var interfaceId) ||
                messageId == null || interfaceId == null)
            {
                return;
            }
            var compilation = await context.Document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);
            var message = DocumentationCommentId.GetFirstSymbolForDeclarationId(messageId, compilation!) as INamedTypeSymbol;
            var serviceInterface = DocumentationCommentId.GetFirstSymbolForDeclarationId(interfaceId, compilation!) as INamedTypeSymbol;
            var declaration = message?.DeclaringSyntaxReferences.FirstOrDefault();
            if (message == null || serviceInterface == null || declaration == null)
            {
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(
                $"Make {message.Name} implement {serviceInterface.Name}",
                cancellationToken => AddInterfaceAsync(context.Document.Project.Solution, declaration, serviceInterface, cancellationToken),
                nameof(AddServiceInterfaceCodeFix)), diagnostic);
        }

        private static async Task<Solution> AddInterfaceAsync(Solution solution, SyntaxReference declaration, INamedTypeSymbol serviceInterface, CancellationToken cancellationToken)
        {
            var document = solution.GetDocument(declaration.SyntaxTree);
            var type = await declaration.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) as TypeDeclarationSyntax;
            if (document == null || type == null)
            {
                return solution;
            }
            var interfaceType = SyntaxFactory.ParseTypeName(serviceInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .WithAdditionalAnnotations(Simplifier.Annotation);
            var withInterface = type.AddBaseListTypes(SyntaxFactory.SimpleBaseType(interfaceType));
            withInterface = withInterface.WithBaseList(withInterface.BaseList!.WithAdditionalAnnotations(Formatter.Annotation));
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return solution.WithDocumentSyntaxRoot(document.Id, root!.ReplaceNode(type, withInterface));
        }
    }
}
