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
    /// OBVS006: runs the async lambda for each message in turn, so they're handled in order and an exception ends the
    /// subscription through its OnError, instead of crashing the app:
    /// <c>stream.Select(m => Observable.FromAsync(async () => ...)).Concat().Subscribe()</c>. Moving the work to a
    /// handler (HandleCommand, HandleEvent or HandleRequest) is better still, but that's in the bus's configuration.
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
            // Only Subscribe(async m => ...) on its own: with OnError or OnCompleted too, they'd need moving as well
            if (lambda == null || subscribe == null || subscribe.ArgumentList.Arguments.Count != 1 ||
                !(subscribe.Expression is MemberAccessExpressionSyntax memberAccess) || ParameterOf(lambda) == null)
            {
                return;
            }

            context.RegisterCodeFix(CodeAction.Create(
                "Run it for each message in turn",
                cancellationToken => RewriteAsync(context.Document, subscribe, memberAccess.Expression, lambda, cancellationToken),
                nameof(SubscribeAsyncCodeFix)), diagnostic);
        }

        private static async Task<Document> RewriteAsync(Document document, InvocationExpressionSyntax subscribe, ExpressionSyntax source,
            LambdaExpressionSyntax lambda, CancellationToken cancellationToken)
        {
            var body = ((CSharpSyntaxNode?)lambda.Block ?? lambda.ExpressionBody)!.WithoutTrivia();
            var rewritten = SyntaxFactory.ParseExpression(
                    $"{source.WithoutTrivia()}.Select({ParameterOf(lambda)} => Observable.FromAsync(async () => {body})).Concat().Subscribe()")
                .WithTriviaFrom(subscribe)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!;
            root = root.ReplaceNode(subscribe, rewritten);
            if (!root.Usings.Any(u => u.Name?.ToString() == "System.Reactive.Linq"))
            {
                root = root.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Reactive.Linq")).WithAdditionalAnnotations(Formatter.Annotation));
            }
            return document.WithSyntaxRoot(root);
        }

        private static string? ParameterOf(LambdaExpressionSyntax lambda)
        {
            return lambda switch
            {
                SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.Text,
                ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.Text,
                _ => null,
            };
        }
    }
}
