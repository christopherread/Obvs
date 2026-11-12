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
    /// <summary>
    /// OBVS006: subscribes the async lambda with <c>SubscribeAsync</c>, which awaits each message in turn, carries on
    /// after a failure, and settles each message once processed: <c>stream.SubscribeAsync(async m =&gt; ...)</c>.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class SubscribeAsyncCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Rules.AsyncVoidSubscriber.Id);

        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var lambda = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<LambdaExpressionSyntax>();
            var subscribe = lambda?.Parent?.Parent?.Parent as InvocationExpressionSyntax;
            // Only Subscribe(async m => ...) on its own: SubscribeAsync's onError is per message, unlike Subscribe's
            if (lambda == null || subscribe == null || subscribe.ArgumentList.Arguments.Count != 1 ||
                !(subscribe.Expression is MemberAccessExpressionSyntax memberAccess))
            {
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(
                "Use SubscribeAsync",
                cancellationToken => RewriteAsync(context.Document, subscribe, memberAccess, cancellationToken),
                nameof(SubscribeAsyncCodeFix)), diagnostic);
        }

        private static async Task<Document> RewriteAsync(Document document, InvocationExpressionSyntax subscribe, MemberAccessExpressionSyntax memberAccess,
            CancellationToken cancellationToken)
        {
            var rewritten = subscribe.WithExpression(memberAccess.WithName(SyntaxFactory.IdentifierName("SubscribeAsync").WithTriviaFrom(memberAccess.Name)));

            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
            root = root.ReplaceNode(subscribe, rewritten);
            if (!root.Usings.Any(u => u.Name?.ToString() == "Obvs"))
            {
                root = root.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Obvs")).WithAdditionalAnnotations(Formatter.Annotation));
            }
            return document.WithSyntaxRoot(root);
        }
    }
}
