using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer
{
    internal static class SyntaxHelper
    {
        public static T FindContainer<T>(this SyntaxNode tokenParent) where T : SyntaxNode
        {
            return tokenParent is T invocation ? invocation : tokenParent.Parent == null ? null : FindContainer<T>(tokenParent.Parent);
        }
    }
}