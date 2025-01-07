﻿namespace ClrHeapAllocationAnalyzer
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System;
    using System.Collections.Immutable;
    using System.Linq;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EnumeratorAllocationAnalyzer : AllocationAnalyzer
    {
        /// <summary>
        /// HAA0401: Possible allocation of reference type enumerator
        /// </summary>
        public static DiagnosticDescriptor ReferenceTypeEnumeratorRule = new DiagnosticDescriptor("HAA0401", "Possible allocation of reference type enumerator", "Non-ValueType enumerator may result in a heap allocation", "Performance", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ReferenceTypeEnumeratorRule);

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.ForEachStatement, SyntaxKind.InvocationExpression };

        private static object[] EmptyMessageArgs { get; } = { };

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;
            if (node is ForEachStatementSyntax foreachExpression)
            {
                var typeInfo = semanticModel.GetTypeInfo(foreachExpression.Expression, cancellationToken);
                if (typeInfo.Type == null)
                    return;

                if (typeInfo.Type.Name == "String" && typeInfo.Type.ContainingNamespace.Name == "System")
                {
                    // Special case for System.String which is optmizined by
                    // the compiler and does not result in an allocation.
                    return;
                }

                // Regular way of getting the enumerator
                ImmutableArray<ISymbol> enumerator = typeInfo.Type.GetMembers("GetEnumerator");
                if ((enumerator == null || enumerator.Length == 0) && typeInfo.ConvertedType != null)
                {
                    // 1st we try and fallback to using the ConvertedType
                    enumerator = typeInfo.ConvertedType.GetMembers("GetEnumerator");
                }
                if ((enumerator == null || enumerator.Length == 0) && typeInfo.Type.Interfaces != null)
                {
                    // 2nd fallback, now we try and find the IEnumerable Interface explicitly
                    var iEnumerable = typeInfo.Type.Interfaces.Where(i => i.Name == "IEnumerable").ToImmutableArray();
                    if (iEnumerable != null && iEnumerable.Length > 0)
                    {
                        enumerator = iEnumerable[0].GetMembers("GetEnumerator");
                    }
                }

                if (enumerator != null && enumerator.Length > 0)
                {
                    // probably should do something better here, hack.
                    if (enumerator[0] is IMethodSymbol methodSymbol)
                    {
                        if (methodSymbol.ReturnType.IsReferenceType && methodSymbol.ReturnType.SpecialType != SpecialType.System_Collections_IEnumerator)
                        {
                            reportDiagnostic(Diagnostic.Create(ReferenceTypeEnumeratorRule, foreachExpression.InKeyword.GetLocation(), EmptyMessageArgs));
                            HeapAllocationAnalyzerEventSource.Logger.EnumeratorAllocation(filePath);
                        }
                    }
                }

                return;
            }

            if (node is InvocationExpressionSyntax invocationExpression)
            {
                var methodInfo = semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol as IMethodSymbol;
                if (methodInfo?.ReturnType != null && methodInfo.ReturnType.IsReferenceType)
                {
                    if (methodInfo.ReturnType.AllInterfaces != null)
                    {
                        foreach (var @interface in methodInfo.ReturnType.AllInterfaces)
                        {
                            if (@interface.SpecialType is SpecialType.System_Collections_Generic_IEnumerator_T or SpecialType.System_Collections_IEnumerator)
                            {
                                reportDiagnostic(Diagnostic.Create(ReferenceTypeEnumeratorRule, invocationExpression.GetLocation(), EmptyMessageArgs));
                                HeapAllocationAnalyzerEventSource.Logger.EnumeratorAllocation(filePath);
                            }
                        }
                    }
                }
            }
        }
    }
}
