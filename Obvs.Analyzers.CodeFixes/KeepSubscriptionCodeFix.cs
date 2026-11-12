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

namespace Obvs.Analyzers.CodeFixes
{
    /// <summary>OBVS005: keeps the subscription in a local, to dispose of when it's no longer wanted.</summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class KeepSubscriptionCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Rules.DiscardedSubscription.Id);

        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var statement = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (statement == null || !(statement.Parent is BlockSyntax))
            {
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(
                "Keep the subscription",
                cancellationToken => KeepAsync(context.Document, statement, cancellationToken),
                nameof(KeepSubscriptionCodeFix)), diagnostic);
        }

        private static async Task<Document> KeepAsync(Document document, ExpressionStatementSyntax statement, CancellationToken cancellationToken)
        {
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var name = "subscription";
            for (var i = 2; model!.LookupSymbols(statement.SpanStart, name: name).Any(); i++)
            {
                name = "subscription" + i;
            }

            var declaration = SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"),
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(name)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(statement.Expression.WithoutTrivia())))))
                .WithTriviaFrom(statement)
                .WithAdditionalAnnotations(Formatter.Annotation);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return document.WithSyntaxRoot(root!.ReplaceNode(statement, declaration));
        }
    }
}
