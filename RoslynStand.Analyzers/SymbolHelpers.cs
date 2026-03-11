using Microsoft.CodeAnalysis;

namespace RoslynStand.Analyzers;

internal static class SymbolHelpers
{
    public static bool IsCancellationToken(this ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
        "global::System.Threading.CancellationToken";
    public static bool IsTaskLike(this ITypeSymbol type)
    {
        string display = type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return display is
            "global::System.Threading.Tasks.Task" or
            "global::System.Threading.Tasks.Task<TResult>" or
            "global::System.Threading.Tasks.ValueTask" or
            "global::System.Threading.Tasks.ValueTask<TResult>";
    }
}
