using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynStand.Analyzers.Syntax;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncMethodNameSyntaxAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RoslynStandDescriptors.AsyncMethodName];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return;
        }

        string methodName = method.Identifier.ValueText;
        if (methodName.EndsWith("Async", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                RoslynStandDescriptors.AsyncMethodName,
                method.Identifier.GetLocation(),
                methodName));
    }
}
