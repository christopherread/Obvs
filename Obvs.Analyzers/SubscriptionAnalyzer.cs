using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Obvs.Analyzers
{
    /// <summary>
    /// Checks each <c>Subscribe</c> or <c>SubscribeAsync</c> to one of the bus's streams, directly or through Rx operators:
    /// OBVS005, the subscription is kept; OBVS006, <c>Subscribe</c> isn't given an async lambda, which would run as async void.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SubscriptionAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rules.DiscardedSubscription, Rules.AsyncVoidSubscriber);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var busClient = start.Compilation.GetTypeByMetadataName("Obvs.IServiceBusClient");
                if (busClient == null)
                {
                    return;
                }
                start.RegisterOperationAction(operationContext => Analyze(operationContext, busClient), OperationKind.Invocation);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol busClient)
        {
            var subscribe = (IInvocationOperation)context.Operation;
            if ((subscribe.TargetMethod.Name != "Subscribe" && subscribe.TargetMethod.Name != "SubscribeAsync") || !FromBus(Source(subscribe), busClient))
            {
                return;
            }

            if (subscribe.Parent is IExpressionStatementOperation)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.DiscardedSubscription, subscribe.Syntax.GetLocation()));
            }

            foreach (var argument in subscribe.Arguments)
            {
                var lambda = (argument.Value as IDelegateCreationOperation)?.Target as IAnonymousFunctionOperation;
                if (lambda != null && lambda.Symbol.IsAsync && lambda.Symbol.ReturnsVoid)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rules.AsyncVoidSubscriber, lambda.Syntax.GetLocation()));
                }
            }
        }

        // What an operator or Subscribe is applied to: its instance, or an extension method's first argument
        private static IOperation? Source(IInvocationOperation call)
        {
            if (call.TargetMethod.IsExtensionMethod)
            {
                return call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value;
            }
            return call.Instance;
        }

        private static bool FromBus(IOperation? operation, INamedTypeSymbol busClient)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }
            switch (operation)
            {
                case IPropertyReferenceOperation property:
                    return IsBus(property.Property.ContainingType, busClient);
                case IInvocationOperation call:
                    return IsBus(call.TargetMethod.ContainingType, busClient) || FromBus(Source(call), busClient);
                default:
                    return false;
            }
        }

        private static bool IsBus(INamedTypeSymbol? type, INamedTypeSymbol busClient)
        {
            return type != null && (SymbolEqualityComparer.Default.Equals(type, busClient) ||
                                    type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, busClient)));
        }
    }
}
