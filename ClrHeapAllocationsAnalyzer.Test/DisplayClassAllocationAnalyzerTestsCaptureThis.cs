using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;

namespace ClrHeapAllocationAnalyzer.Test
{
    /// <summary>
    /// Tests around the special treatment of the capture of "this" by the compiler.
    /// </summary>
    [TestClass]
    public class DisplayClassAllocationAnalyzerTestsCaptureThis : AllocationAnalyzerTests
    {
        /// <summary>
        /// Test of the special case for only one captured field called 'this'.
        /// </summary>
        [TestMethod]
        public void DisplayClassAllocationCase1ThisNotCaptured()
        {
            var sampleProgramNoThisCapture =
@"using System;

class NoThisCapture
{
    void TestFunction()
    {
        var action = new System.Action(() =>
        {
            this.TestFunction();
        });
    }
}
";
            var analyzer = new DisplayClassAllocationAnalyzer();
            var infoNoCapture = ProcessCode(analyzer, sampleProgramNoThisCapture, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression));

            // There is no display class created.
            Assert.AreEqual(0, infoNoCapture.Allocations.Count);
        }

        [TestMethod]
        public void DisplayClassAllocationCase2ThisCaptured()
        {

            var sampleProgramThisCapture =
@"using System;

class ThisCapture
{
    void TestFunction(int parameter)
    {
        int localVariable = 5;
        var action = new System.Action(() =>
        {
            this.TestFunction(localVariable);
        });
    }
}
";
            var analyser = new DisplayClassAllocationAnalyzer();

            var info = ProcessCode(analyser, sampleProgramThisCapture, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression));
            Assert.AreEqual(3, info.Allocations.Count);

            // Diagnostic: warning HeapAnalyzerClosureSourceRule: HAA0301 Heap allocation of closure Captures: word
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureDriverRule.Id, line: 8, character: 43);
            // Diagnostic: warning HeapAnalyzerClosureCaptureRule: HAA0302 The compiler will emit a class that will hold this as a field to allow capturing of this closure
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureCaptureRule.Id, line: 5, character: 10);
            AssertEx.ContainsDiagnostic(info.Allocations, id: DisplayClassAllocationAnalyzer.ClosureCaptureRule.Id, line: 7, character: 13);
        }

        [TestMethod]
        public void DisplayClassAllocationCase3ThisCaptured()
        {
            var sampleProgramThisCapture =
@"using System;

class ThisCaptureTwo
{
    void TestFunction(int parameter)
    {
        // Make sure the lambda that does not require capturing is in its own scope
        {
            var action1 = new System.Action(() =>
            {
                this.TestFunction(66);
            });
        }

        // Make sure the lambda that DOES require capturing is in its own scope
        {
            int localVariable = 5;
            var action2 = new System.Action(() =>
            {
                this.TestFunction(localVariable);
            });
        }
    }
}
";

            var analyzer = new DisplayClassAllocationAnalyzer();
            var info = ProcessCode(analyzer, sampleProgramThisCapture, ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression));

            // 28.12.2024 Related Issue untriaged
            // Should be 2. Are actually 4 right now.
            // FlowAnalysis claims that localVariable is captured in the scope that does not need it.
            // Currently "Intended Design" for simplicity according to MS.
            // https://github.com/dotnet/roslyn/issues/76573
            // So the analyzer can only mark FlowAnalysis claims.
            Assert.AreEqual(4, info.Allocations.Count);

            // Depending on the use of scope, the compiler will or will not optimize the lambda that does not require more than just "this",
            // but the analyzer can not reflect that currently.
        }

        /// <summary>
        /// Testcase: No DisplayClass created. (according to Sharplab.io)
        /// Code for TestCase 1.
        /// </summary>
        class NoThisCapture
        {
            void TestFunction()
            {
                var action = new System.Action(() =>
                {
                    this.TestFunction();
                });
            }
        }

        /// <summary>
        /// Testcase: DisplayClass for this and localVariable. (according to Sharplab.io)
        /// Code for TestCase 2.
        /// </summary>
        class ThisAndParameterCapture
        {
            void TestFunction(int parameter)
            {
                int localVariable = 5;
                var action = new System.Action(() =>
                {
                    this.TestFunction(localVariable);
                });
            }
        }

        /// <summary>
        /// Testcase: DisplayClass for this and localVariable. (according to Sharplab.io)
        /// Code for TestCase 3.
        /// </summary>
        class ThisCaptureTwo
        {
            void TestFunction(int parameter)
            {
                // Make sure the lambda that does not require capturing is in its own scope
                {
                    var action1 = new System.Action(() =>
                    {
                        this.TestFunction(66);
                    });
                }

                // Make sure the lambda that DOES require capturing is in its own scope
                {
                    int localVariable = 5;
                    var action2 = new System.Action(() =>
                    {
                        this.TestFunction(localVariable);
                    });
                }
            }
        }
    }
}
