﻿using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace ClrHeapAllocationAnalyzer
{
    public class AllocationRules
    {
        private static HashSet<ValueTuple<string, string>> IgnoredAttributes { get; } =
        [
            ("System.Runtime.CompilerServices", "CompilerGeneratedAttribute"),
            ("System.CodeDom.Compiler", "GeneratedCodeAttribute")
        ];

        public static bool IsIgnoredFile(string filePath)
        {
            return filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsIgnoredAttribute(AttributeData attribute)
        {
            return IgnoredAttributes.Contains((attribute.AttributeClass.ContainingNamespace.ToString(), attribute.AttributeClass.Name));
        }
    }
}
