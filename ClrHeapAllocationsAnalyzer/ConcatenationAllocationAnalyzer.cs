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
    public sealed class ConcatenationAllocationAnalyzer : AllocationAnalyzer
    {
        /// <summary>
        /// HAA0201: Implicit string concatenation allocation
        /// </summary>
        public static DiagnosticDescriptor StringConcatenationAllocationRule = new DiagnosticDescriptor("HAA0201", "Implicit string concatenation allocation", "Considering using StringBuilder", "Performance", DiagnosticSeverity.Warning, true, string.Empty, "http://msdn.microsoft.com/en-us/library/2839d5h5(v=vs.110).aspx");

        /// <summary>
        /// HAA0202: Value type to reference type conversion allocation for string concatenation
        /// </summary>
        public static DiagnosticDescriptor ValueTypeToReferenceTypeInAStringConcatenationRule = new DiagnosticDescriptor("HAA0202", "Value type to reference type conversion allocation for string concatenation", "Value type ({0}) is being boxed to a reference type for a string concatenation", "Performance", DiagnosticSeverity.Warning, true, string.Empty, "http://msdn.microsoft.com/en-us/library/yz2be5wk.aspx");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(StringConcatenationAllocationRule, ValueTypeToReferenceTypeInAStringConcatenationRule);

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression };

        private static object[] EmptyMessageArgs { get; } = { };

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {

            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;
            var binaryExpressions = node.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Reverse(); // need inner most expressions

            int stringConcatenationCount = 0;
            foreach (var binaryExpression in binaryExpressions)
            {
                if (binaryExpression.Left == null || binaryExpression.Right == null)
                {
                    continue;
                }

                bool isConstant = semanticModel.GetConstantValue(binaryExpression, cancellationToken).HasValue;
                if (isConstant)
                {
                    continue;
                }

                var left = semanticModel.GetTypeInfo(binaryExpression.Left, cancellationToken);
                var right = semanticModel.GetTypeInfo(binaryExpression.Right, cancellationToken);

                // regular string allocation
                if (left.Type?.SpecialType == SpecialType.System_String || right.Type?.SpecialType == SpecialType.System_String)
                {
                    stringConcatenationCount++;
                }
            }

            if (stringConcatenationCount > 3)
            {
                reportDiagnostic(Diagnostic.Create(StringConcatenationAllocationRule, node.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.StringConcatenationAllocation(filePath);
            }
        }

        private static void CheckTypeConversion(TypeInfo typeInfo, Conversion conversionInfo, Action<Diagnostic> reportDiagnostic, Location location, string filePath)
        {

            static bool IsOptimizedValueType(ITypeSymbol type)
            {
                return type.SpecialType is SpecialType.System_Boolean or
                       SpecialType.System_Char or
                       SpecialType.System_IntPtr or
                       SpecialType.System_UIntPtr;
            }

            if (conversionInfo.IsBoxing && !IsOptimizedValueType(typeInfo.Type))
            {
                reportDiagnostic(Diagnostic.Create(ValueTypeToReferenceTypeInAStringConcatenationRule, location, new[] { typeInfo.Type.ToDisplayString() }));
                HeapAllocationAnalyzerEventSource.Logger.BoxingAllocationInStringConcatenation(filePath);
            }
        }
    }
}