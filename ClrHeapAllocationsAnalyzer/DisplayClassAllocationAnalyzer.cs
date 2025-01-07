namespace ClrHeapAllocationAnalyzer
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DisplayClassAllocationAnalyzer : AllocationAnalyzer
    {
        /// <summary>
        /// HAA0301: Closure Allocation Source
        /// </summary>
        public static DiagnosticDescriptor ClosureDriverRule = new DiagnosticDescriptor("HAA0301", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", "Performance", DiagnosticSeverity.Warning, true);

        /// <summary>
        /// HAA0302: Display class allocation to capture closure
        /// </summary>
        public static DiagnosticDescriptor ClosureCaptureRule = new DiagnosticDescriptor("HAA0302", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", "Performance", DiagnosticSeverity.Warning, true);

        /// <summary>
        /// HAA0303: Lambda or anonymous method in a generic method allocates a delegate instance
        /// </summary>
        public static DiagnosticDescriptor LambaOrAnonymousMethodInGenericMethodRule = new DiagnosticDescriptor("HAA0303", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", "Performance", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClosureCaptureRule, ClosureDriverRule, LambaOrAnonymousMethodInGenericMethodRule, TypeConversionAllocationAnalyzer.MethodGroupAllocationRule);

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression };

        private static object[] EmptyMessageArgs { get; } = { };

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;

            var anonExpr = node as AnonymousMethodExpressionSyntax;
            if (anonExpr?.Block?.ChildNodes() != null && anonExpr.Block.ChildNodes().Any())
            {
                GenericMethodCheck(semanticModel, node, anonExpr.DelegateKeyword.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(anonExpr.Block.ChildNodes().First(), anonExpr.Block.ChildNodes().Last()), reportDiagnostic, anonExpr.DelegateKeyword.GetLocation());
                return;
            }

            if (node is SimpleLambdaExpressionSyntax lambdaExpr)
            {
                GenericMethodCheck(semanticModel, node, lambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(lambdaExpr), reportDiagnostic, lambdaExpr.ArrowToken.GetLocation());
                return;
            }

            if (node is ParenthesizedLambdaExpressionSyntax parenLambdaExpr)
            {
                GenericMethodCheck(semanticModel, node, parenLambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                bool isConstructor = node.Parent.Parent.Parent is ObjectCreationExpressionSyntax;
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(parenLambdaExpr), reportDiagnostic, parenLambdaExpr.ArrowToken.GetLocation(), isConstructor);
                return;
            }
        }

        private static void ClosureCaptureDataFlowAnalysis(DataFlowAnalysis flow, Action<Diagnostic> reportDiagnostic, Location location, bool isConstructor = false)
        {
            if (flow?.Captured.Length <= 0)
            {
                return;
            }

            if (flow.Captured.Length == 1 && flow.Captured[0].Name == "this")
            {
                if (!isConstructor)
                {
                    reportDiagnostic(Diagnostic.Create(TypeConversionAllocationAnalyzer.MethodGroupAllocationRule, flow.Captured[0].Locations[0], EmptyMessageArgs));
                }

                // If only 'this' is captured, code generated will not be in a display class.
                return;
            }

            foreach (var capture in flow.Captured)
            {
                if (capture.Name != null && capture.Locations != null)
                {
                    foreach (var l in capture.Locations)
                    {
                        reportDiagnostic(Diagnostic.Create(ClosureCaptureRule, l, EmptyMessageArgs));
                    }
                }
            }

            reportDiagnostic(Diagnostic.Create(ClosureDriverRule, location, new[] { string.Join(",", flow.Captured.Select(x => x.Name)) }));
        }

        private static void GenericMethodCheck(SemanticModel semanticModel, SyntaxNode node, Location location, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol != null)
            {
                if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol.ContainingSymbol is IMethodSymbol containingSymbol && containingSymbol.Arity > 0)
                {
                    reportDiagnostic(Diagnostic.Create(LambaOrAnonymousMethodInGenericMethodRule, location, EmptyMessageArgs));
                }
            }
        }
    }
}