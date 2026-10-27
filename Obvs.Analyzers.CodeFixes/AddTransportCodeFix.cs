using System.Collections.Generic;
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
    /// OBVS004: adds a transport to the service, one fix for each transport the project can use: a
    /// <c>Use&lt;Transport&gt;(Action&lt;Options&gt;)</c> extension of the service builder. Its options are left for you to set.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class AddTransportCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Rules.NoTransport.Id);

        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var addService = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<InvocationExpressionSyntax>();
            var lambda = addService?.ArgumentList.Arguments.Select(argument => argument.Expression).OfType<LambdaExpressionSyntax>().FirstOrDefault();
            var compilation = await context.Document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);
            var service = compilation?.GetTypeByMetadataName("Obvs.Hosting.ObvsServiceBuilder");
            if (lambda == null || service == null)
            {
                return;
            }

            foreach (var transport in Transports(compilation!, service))
            {
                context.RegisterCodeFix(CodeAction.Create(
                    $"Add {transport}(...)",
                    cancellationToken => AddTransportAsync(context.Document, lambda, transport, service, cancellationToken),
                    nameof(AddTransportCodeFix) + transport), diagnostic);
            }
        }

        private static async Task<Document> AddTransportAsync(Document document, LambdaExpressionSyntax lambda, string transport, INamedTypeSymbol service,
            CancellationToken cancellationToken)
        {
            var parameter = lambda is SimpleLambdaExpressionSyntax simple
                ? simple.Parameter.Identifier.Text
                : ((ParenthesizedLambdaExpressionSyntax)lambda).ParameterList.Parameters.First().Identifier.Text;
            var call = $"{transport}(options => {{ }})";

            LambdaExpressionSyntax updated;
            if (lambda.Block != null)
            {
                var statement = SyntaxFactory.ParseStatement($"{parameter}.{call};");
                updated = lambda.WithBlock(lambda.Block.WithStatements(lambda.Block.Statements.Insert(0, statement)));
            }
            else
            {
                var body = lambda.ExpressionBody!;
                var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var bodyType = model!.GetTypeInfo(body, cancellationToken).Type;
                updated = SymbolEqualityComparer.Default.Equals(bodyType, service)
                    // service => service.AsServer() becomes service => service.AsServer().UseRabbitMq(options => { })
                    ? lambda.WithExpressionBody(SyntaxFactory.ParseExpression($"{body.WithoutTrivia()}.{call}"))
                    : lambda.WithExpressionBody(null).WithBlock(SyntaxFactory.Block(
                        SyntaxFactory.ParseStatement($"{parameter}.{call};"),
                        SyntaxFactory.ExpressionStatement(body.WithoutTrivia())));
            }

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return document.WithSyntaxRoot(root!.ReplaceNode(lambda, updated.WithAdditionalAnnotations(Formatter.Annotation)));
        }

        // Use<Transport>(this ObvsServiceBuilder, Action<TOptions>) extensions, in the project and the Obvs packages it references
        private static IEnumerable<string> Transports(Compilation compilation, INamedTypeSymbol service)
        {
            var assemblies = new[] { compilation.Assembly }.Concat(compilation.SourceModule.ReferencedAssemblySymbols
                .Where(assembly => assembly.Name.StartsWith("Obvs")));
            return assemblies
                .SelectMany(assembly => Types(assembly.GlobalNamespace))
                .Where(type => type.IsStatic)
                .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>())
                .Where(method => method.IsExtensionMethod && method.Name.StartsWith("Use") && method.Parameters.Length == 2 &&
                                 SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, service) &&
                                 method.Parameters[1].Type is INamedTypeSymbol { Name: "Action", TypeArguments.Length: 1 })
                .Select(method => method.Name)
                .Distinct()
                .OrderBy(name => name);
        }

        private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                yield return type;
            }
            foreach (var nested in ns.GetNamespaceMembers())
            {
                foreach (var type in Types(nested))
                {
                    yield return type;
                }
            }
        }
    }
}
