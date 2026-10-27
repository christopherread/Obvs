using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Obvs.Analyzers
{
    /// <summary>
    /// Checks each <c>AddService&lt;TServiceMessage&gt;(name, service => ...)</c>: OBVS001, its messages and handlers belong to
    /// it; OBVS003, its request handlers answer with its own responses; OBVS004, it has a transport.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ServiceConfigurationAnalyzer : DiagnosticAnalyzer
    {
        public const string MessageTypeProperty = "MessageType";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rules.MessageOfAnotherService, Rules.ResponseOfAnotherService, Rules.NoTransport);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var bus = start.Compilation.GetTypeByMetadataName("Obvs.Hosting.ObvsBuilder");
                var service = start.Compilation.GetTypeByMetadataName("Obvs.Hosting.ObvsServiceBuilder");
                if (bus == null || service == null)
                {
                    return;
                }
                start.RegisterOperationAction(operationContext => Analyze(operationContext, bus, service), OperationKind.Invocation);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol bus, INamedTypeSymbol service)
        {
            var addService = (IInvocationOperation)context.Operation;
            var method = addService.TargetMethod;
            if (method.Name != "AddService" || !SymbolEqualityComparer.Default.Equals(method.ContainingType, bus) || method.TypeArguments.Length != 1)
            {
                return;
            }
            var serviceInterface = method.TypeArguments[0];
            var serviceName = addService.Arguments.FirstOrDefault(a => a.Parameter?.Name == "name")?.Value.ConstantValue.Value as string ?? "?";
            var configure = addService.Arguments.FirstOrDefault(a => a.Parameter?.Name == "configure")?.Value;
            var lambda = (configure as IDelegateCreationOperation)?.Target as IAnonymousFunctionOperation;
            if (lambda == null)
            {
                return;
            }

            var hasTransport = false;
            foreach (var call in lambda.Body.Descendants().OfType<IInvocationOperation>())
            {
                var target = call.TargetMethod;
                var onService = SymbolEqualityComparer.Default.Equals(target.ContainingType, service) ||
                                target.IsExtensionMethod && target.Parameters.Length > 0 && SymbolEqualityComparer.Default.Equals(target.Parameters[0].Type, service);
                if (!onService)
                {
                    continue;
                }

                if (target.Name.StartsWith("Use") && SymbolEqualityComparer.Default.Equals(target.ReturnType, service))
                {
                    hasTransport = true;
                }
                else if (target.Name == "AddMessages")
                {
                    foreach (var typeOf in call.Descendants().OfType<ITypeOfOperation>())
                    {
                        CheckBelongs(context, typeOf.TypeOperand, serviceInterface, serviceName, typeOf.Syntax.GetLocation());
                    }
                }
                else if (target.Name.StartsWith("Handle") && target.TypeArguments.Length > 0)
                {
                    var location = NameLocation(call);
                    CheckBelongs(context, target.TypeArguments[0], serviceInterface, serviceName, location);
                    if (target.Name == "HandleRequest" && target.TypeArguments.Length > 1 && !Implements(target.TypeArguments[1], serviceInterface))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rules.ResponseOfAnotherService, location,
                            serviceName, target.TypeArguments[0].Name, target.TypeArguments[1].Name, serviceInterface.Name));
                    }
                }
            }

            if (!hasTransport)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.NoTransport, NameLocation(addService), serviceName));
            }
        }

        private static void CheckBelongs(OperationAnalysisContext context, ITypeSymbol message, ITypeSymbol serviceInterface, string serviceName, Location location)
        {
            if (Implements(message, serviceInterface))
            {
                return;
            }
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add(Rules.ServiceInterfaceProperty, DocumentationCommentId.CreateDeclarationId(serviceInterface))
                .Add(MessageTypeProperty, DocumentationCommentId.CreateDeclarationId(message));
            context.ReportDiagnostic(Diagnostic.Create(Rules.MessageOfAnotherService, location, properties, serviceName, message.Name, serviceInterface.Name));
        }

        private static bool Implements(ITypeSymbol type, ITypeSymbol serviceInterface)
        {
            return SymbolEqualityComparer.Default.Equals(type, serviceInterface) ||
                   type.AllInterfaces.Contains(serviceInterface as INamedTypeSymbol, SymbolEqualityComparer.Default);
        }

        // The method's name, as in service.HandleCommand<PlaceOrder>, rather than the whole call
        private static Location NameLocation(IInvocationOperation call)
        {
            var syntax = call.Syntax as InvocationExpressionSyntax;
            return syntax?.Expression is MemberAccessExpressionSyntax memberAccess ? memberAccess.Name.GetLocation() : call.Syntax.GetLocation();
        }
    }
}
