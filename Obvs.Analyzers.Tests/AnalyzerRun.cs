using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Obvs.Analyzers;
using Obvs.Analyzers.CodeFixes;

namespace Obvs.Analyzers.Tests
{
    // Compiles source the way a project referencing Obvs does, with Obvs's analyzers, and applies their code fixes
    internal static class AnalyzerRun
    {
        // Message types and a stand-in transport the tests' sources use
        public const string Prelude = @"
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs;
using Obvs.Hosting;
using Obvs.Types;

namespace Contoso.Orders
{
    public interface IOrderMessage : IMessage { }
    public interface IShippingMessage : IMessage { }
    public class PlaceOrder : IOrderMessage, ICommand { }
    public class OrderPlaced : IOrderMessage, IEvent { }
    public class GetQuote : IOrderMessage, IRequest { public string RequestId { get; set; } public string RequesterId { get; set; } }
    public class Quote : IOrderMessage, IResponse { public string RequestId { get; set; } public string RequesterId { get; set; } }
    public class ShipOrder : IShippingMessage, ICommand { }
    public class ShippingQuote : IShippingMessage, IResponse { public string RequestId { get; set; } public string RequesterId { get; set; } }
    public class FakeOptions { }
    public static class FakeTransport
    {
        public static ObvsServiceBuilder UseFake(this ObvsServiceBuilder service, Action<FakeOptions> configure) => service;
    }
}
";

        private static readonly DiagnosticAnalyzer[] Analyzers =
        {
            new MessageKindAnalyzer(), new ServiceConfigurationAnalyzer(), new SubscriptionAnalyzer(),
        };

        private static readonly CodeFixProvider[] CodeFixes =
        {
            new AddServiceInterfaceCodeFix(), new AddTransportCodeFix(), new KeepSubscriptionCodeFix(), new SubscribeAsyncCodeFix(),
        };

        public static async Task<ImmutableArray<Diagnostic>> DiagnosticsAsync(string source)
        {
            var document = Document(source);
            return await DiagnosticsAsync(document);
        }

        // Applies the code fix titled title for the first diagnostic with id, and returns the source it leaves
        public static async Task<string> FixAsync(string source, string id, string title = null)
        {
            var document = Document(source);
            var diagnostic = (await DiagnosticsAsync(document)).First(d => d.Id == id);
            var actions = ImmutableArray.CreateBuilder<CodeAction>();
            foreach (var fix in CodeFixes.Where(fix => fix.FixableDiagnosticIds.Contains(id)))
            {
                await fix.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
            }
            var chosen = title == null ? actions.Single() : actions.Single(action => action.Title == title);
            var operations = await chosen.GetOperationsAsync(CancellationToken.None);
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            return (await solution.GetDocument(document.Id).GetTextAsync()).ToString();
        }

        // The titles of the code fixes offered for the first diagnostic with id
        public static async Task<string[]> FixTitlesAsync(string source, string id)
        {
            var document = Document(source);
            var diagnostic = (await DiagnosticsAsync(document)).First(d => d.Id == id);
            var titles = ImmutableArray.CreateBuilder<string>();
            foreach (var fix in CodeFixes.Where(fix => fix.FixableDiagnosticIds.Contains(id)))
            {
                await fix.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => titles.Add(action.Title), CancellationToken.None));
            }
            return titles.ToArray();
        }

        // Ignores differences in whitespace, which formatting decides
        public static string Normalize(string source) => Regex.Replace(source, @"\s+", " ").Trim();

        private static async Task<ImmutableArray<Diagnostic>> DiagnosticsAsync(Document document)
        {
            var compilation = await document.Project.GetCompilationAsync();
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidOperationException("The test's source doesn't compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
            }
            return await compilation.WithAnalyzers(ImmutableArray.Create(Analyzers)).GetAnalyzerDiagnosticsAsync();
        }

        private static Document Document(string source)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Contoso.Orders", "Contoso.Orders",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12),
                metadataReferences: references));
            return workspace.AddDocument(project.Id, "Test.cs", SourceText.From(Prelude + source));
        }
    }
}
