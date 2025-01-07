﻿
/* Unmerged change from project 'ClrHeapAllocationsAnalyzer.Test (net5.0)'
Before:
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
After:
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Diagnostics;
using System.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Threading.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Reflection;
using System.Threading.Tasks;
*/
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace ClrHeapAllocationAnalyzer.Test
{
    public abstract class AllocationAnalyzerTests
    {
        protected static readonly List<MetadataReference> References = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(int).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IList<>).Assembly.Location),
                //MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            };

        protected IList<SyntaxNode> GetExpectedDescendants(IEnumerable<SyntaxNode> nodes, ImmutableArray<SyntaxKind> expected)
        {
            var descendants = new List<SyntaxNode>();
            foreach (var node in nodes)
            {
                if (expected.Any(e => e == node.Kind()))
                {
                    descendants.Add(node);
                    continue;
                }

                foreach (var child in node.ChildNodes())
                {
                    if (expected.Any(e => e == child.Kind()))
                    {
                        descendants.Add(child);
                        continue;
                    }

                    if (child.ChildNodes().Any())
                        descendants.AddRange(GetExpectedDescendants(child.ChildNodes(), expected));
                }
            }
            return descendants;
        }

        protected Info ProcessCode(DiagnosticAnalyzer analyzer, string sampleProgram,
            ImmutableArray<SyntaxKind> expected, bool allowBuildErrors = false, string filePath = "",
            Microsoft.CodeAnalysis.CSharp.LanguageVersion languageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest)
        {

#if NET40_OR_GREATER
            SetEntryAssembly(System.Reflection.Assembly.GetCallingAssembly());
#endif

            if (languageVersion == LanguageVersion.Latest)
            {
#if NET48_OR_GREATER
                languageVersion = LanguageVersion.CSharp7_3;
#elif !NET6_0_OR_GREATER
                languageVersion = LanguageVersion.CSharp9;
#endif
            }

            var options = new CSharpParseOptions(kind: SourceCodeKind.Script, languageVersion: languageVersion);
            var tree = CSharpSyntaxTree.ParseText(sampleProgram, options, filePath);


            // Fix CS0012 problems: https://github.com/dotnet/roslyn/issues/49498#issuecomment-734452762
            Assembly.GetEntryAssembly()
                    .GetReferencedAssemblies()
                    .ToList()
                    .ForEach(a => References.Add(MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            var compilation = CSharpCompilation.Create("Test", new[] { tree }, References);

            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                var msg = "There were Errors in the sample code\n";
                if (allowBuildErrors == false)
                    Assert.Fail(msg + string.Join("\n", diagnostics));
                else
                    Console.WriteLine(msg + string.Join("\n", diagnostics));
            }

            var semanticModel = compilation.GetSemanticModel(tree);
            var matches = GetExpectedDescendants(tree.GetRoot().ChildNodes(), expected);

            // Run the code tree through the analyzer and record the allocations it reports
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
            var allocations = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult().Distinct(DiagnosticEqualityComparer.Instance).ToList();

            return new Info
            {
                Options = options,
                Tree = tree,
                Compilation = compilation,
                Diagnostics = diagnostics,
                SemanticModel = semanticModel,
                Matches = matches,
                Allocations = allocations,
            };
        }

        /// <summary>
        /// Allows setting the Entry Assembly when needed.
        /// Use AssemblyUtilities.SetEntryAssembly() as first line in XNA ad hoc tests
        /// </summary>
        /// <param name="assembly">Assembly to set as entry assembly</param>
        public static void SetEntryAssembly(System.Reflection.Assembly assembly)
        {
#if NET40_OR_GREATER
            AppDomainManager manager = new AppDomainManager();
            FieldInfo entryAssemblyfield = manager.GetType().GetField("m_entryAssembly", BindingFlags.Instance | BindingFlags.NonPublic);
            entryAssemblyfield.SetValue(manager, assembly);

            AppDomain domain = AppDomain.CurrentDomain;
            FieldInfo domainManagerField = domain.GetType().GetField("_domainManager", BindingFlags.Instance | BindingFlags.NonPublic);
            domainManagerField.SetValue(domain, manager);
#endif
        }

        protected class Info
        {
            public CSharpParseOptions Options { get; set; }
            public SyntaxTree Tree { get; set; }
            public CSharpCompilation Compilation { get; set; }
            public ImmutableArray<Diagnostic> Diagnostics { get; set; }
            public SemanticModel SemanticModel { get; set; }
            public IList<SyntaxNode> Matches { get; set; }
            public List<Diagnostic> Allocations { get; set; }
        }
    }
}
