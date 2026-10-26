using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Obvs.Types;

namespace Obvs.Generators.Tests
{
    // Compiles source with the generator, the way a project referencing Obvs does, then loads the result to read
    // what each registry's Types property lists
    internal sealed class GeneratorRun
    {
        private readonly Compilation _output;
        private readonly AssemblyLoadContext _context;

        private GeneratorRun(Compilation output, ImmutableArray<Diagnostic> diagnostics, AssemblyLoadContext context)
        {
            _output = output;
            Diagnostics = diagnostics;
            _context = context;
        }

        // From the generator, and from compiling its output
        public ImmutableArray<Diagnostic> Diagnostics { get; }

        public IEnumerable<Diagnostic> Errors => Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        public static GeneratorRun Of(string source, params byte[][] referencedAssemblies)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .Append(MetadataReference.CreateFromFile(typeof(IMessage).Assembly.Location))
                .Concat(referencedAssemblies.Select(image => MetadataReference.CreateFromImage(image)));
            var compilation = CSharpCompilation.Create("Contoso.Orders",
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12)) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            CSharpGeneratorDriver.Create(new MessagesGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            var diagnostics = generatorDiagnostics.AddRange(emitted.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
            if (!emitted.Success)
            {
                return new GeneratorRun(output, diagnostics, null);
            }

            var context = new AssemblyLoadContext(null, isCollectible: true);
            foreach (var referenced in referencedAssemblies)
            {
                context.LoadFromStream(new MemoryStream(referenced));
            }
            image.Position = 0;
            context.LoadFromStream(image);
            return new GeneratorRun(output, diagnostics, context);
        }

        // Compiles a separate assembly for a run to reference
        public static byte[] Library(string name, string source)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .Append(MetadataReference.CreateFromFile(typeof(IMessage).Assembly.Location));
            var compilation = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var image = new MemoryStream();
            var emitted = compilation.Emit(image);
            if (!emitted.Success)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));
            }
            return image.ToArray();
        }

        // The full names of the types the registry lists, in order
        public string[] TypesOf(string registry)
        {
            if (_context == null)
            {
                throw new InvalidOperationException("The output didn't compile: " + string.Join(Environment.NewLine, Errors));
            }

            var type = _context.Assemblies.Single(assembly => assembly.GetName().Name == "Contoso.Orders").GetType(registry, throwOnError: true);
            var types = (Type[])type.GetProperty("Types").GetValue(null);
            return types.Select(listed => listed.FullName).ToArray();
        }

        // The source an error points at, such as the name of the class it's about
        public string ErrorSourceText(Diagnostic error) => error.Location.SourceTree.ToString().Substring(error.Location.SourceSpan.Start, error.Location.SourceSpan.Length);

        public string GeneratedSource => string.Join(Environment.NewLine, _output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));
    }
}
