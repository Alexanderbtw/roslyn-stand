using Microsoft.CodeAnalysis;

namespace RoslynStand.Analyzers;

internal static class SymbolHelpers
{
    extension(ITypeSymbol type)
    {
        public bool IsCancellationToken() =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
            "global::System.Threading.CancellationToken";
        public bool IsTaskLike()
        {
            string display = type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            return display is
                "global::System.Threading.Tasks.Task" or
                "global::System.Threading.Tasks.Task<TResult>" or
                "global::System.Threading.Tasks.ValueTask" or
                "global::System.Threading.Tasks.ValueTask<TResult>";
        }
    }
}
