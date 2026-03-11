using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynStand.Analyzers.Symbol;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenSymbolAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RoslynStandDescriptors.MissingCancellationToken];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.MethodKind != MethodKind.Ordinary ||
            method.DeclaredAccessibility != Accessibility.Public ||
            !method.ReturnType.IsTaskLike())
        {
            return;
        }

        if (method.Parameters.Any(parameter
                => parameter.Type.IsCancellationToken()))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                RoslynStandDescriptors.MissingCancellationToken,
                method.Locations[0],
                method.Name));
    }
}
