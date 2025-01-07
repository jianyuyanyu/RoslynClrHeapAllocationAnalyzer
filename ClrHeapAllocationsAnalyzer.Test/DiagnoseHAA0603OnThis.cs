using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class DiagnoseHAA0603OnThis : AllocationAnalyzerTests
    {
        [TestMethod]
        public void LambdaWithCallToNonStaticThisMember_GeneratesHAA0603()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo1 {

                    public int CountEvens(List<int> list) {
                        // HAA0603: Heap allocation of Func<int, bool> delegate
                        return list.Count(i => IsEven(i));
                    }

                    // Filter method, non static
                    private bool IsEven(int value) {
                        return value % 2 == 0;
                    }
                }
            ";

            var analyzer = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.AreEqual(1, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 8, character: 32);
        }

        [TestMethod]
        public void DelegateInferenceToNonStaticThisMethod_GeneratesHAA0603()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo1 {

                    public int CountEvens(List<int> list) {
                        // HAA0603: Heap allocation of Func<int, bool> delegate
                        return list.Count(IsEven);
                    }

                    // Filter method, non static
                    private bool IsEven(int value) {
                        return value % 2 == 0;
                    }
                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.AreEqual(1, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 10, character: 43);
        }

        [TestMethod]
        public void LambdaWithNoCallToThis_DoestntGenerateHAA0603()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo2 {

                    public int CountEvens(List<int> list) {
                        return list.Count(i => i % 2 == 0);
                    }

                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void LambdaWithCallToStaticThisMember_DoesntGenerateHAA0603()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo3 {

                    public int CountEvens(List<int> list) {
                        // No warning
                        return list.Count(i => IsEven(i));
                    }

                    // Filter method, static
                    private static bool IsEven(int value) {
                        return value % 2 == 0;
                    }
                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void LambdaWithCallToNonStaticLocalFunction_DoesntGenerateHAA0603()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo4 {

                    public int CountEvens(List<int> list) {

                        // Filter local function, not static
                        bool IsEven(int value) {
                            return value % 2 == 0;
                        }

                        // No warning
                        return list.Count(i => IsEven(i));
                    }
                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void DelegateInferenceToNonStaticLocalFunction_GeneratesHAA0603()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo5 {

                    public int CountEvens(List<int> list) {

                        // Filter local function, not static
                        bool IsEven(int value) {
                            return value % 2 == 0;
                        }

                        // HAA0603: Heap allocation of Func<int, bool> delegate
                        return list.Count(IsEven);
                    }
                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression));

            Assert.AreEqual(1, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 16, character: 43);
        }

        [TestMethod]
        public void DelegateInferenceToStaticLocalFunction_DoesntGenerateHAA0603_usingCSharp11()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo6 {

                    public int CountEvens(List<int> list) {

                        // Filter local function, static
                        static bool IsEven(int value) {
                            return value % 2 == 0;
                        }

                        // No warning
                        return list.Count(IsEven);
                    }
                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();

            // C# 11
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression), languageVersion: LanguageVersion.CSharp11);
            Assert.AreEqual(0, info.Allocations.Count);
        }

        [TestMethod]
        public void DelegateInferenceToStaticLocalFunction_GeneratesHAA0603_usingCSharp10()
        {
            const string snippet = @"
                using System;
                using System.Linq;
                using System.Collections.Generic;

                class Demo6 {

                    public int CountEvens(List<int> list) {
                        // Filter local function, static
                        static bool IsEven(int value) {
                            return value % 2 == 0;
                        }

                        // In C# 10.0 => HAA0603: Heap allocation of Func<int, bool> delegate
                        return list.Count(IsEven);
                    }
                }
            ";

            var analyzer = new TypeConversionAllocationAnalyzer();

            // C# 10
            var info = ProcessCode(analyzer, snippet, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression), languageVersion: LanguageVersion.CSharp10);
            Assert.AreEqual(1, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 15, character: 43);
        }
    }
}
