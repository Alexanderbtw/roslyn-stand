using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RoslynStand.Analyzers.Semantic;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskResultUsageOperationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RoslynStandDescriptors.TaskResultUsage];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;
        if (propertyReference.SemanticModel is null)
        {
            return;
        }

        if (propertyReference.Property.Name != "Result" ||
            propertyReference.Instance?.Type is not { } instanceType ||
            !instanceType.IsTaskLike())
        {
            return;
        }

        if (!IsInsideAsyncMethod(
                propertyReference.SemanticModel,
                propertyReference.Syntax,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                descriptor: RoslynStandDescriptors.TaskResultUsage,
                location: propertyReference.Syntax.GetLocation(),
                messageArgs: GetEnclosingMethodName(
                    propertyReference.SemanticModel,
                    propertyReference.Syntax,
                    context.CancellationToken)));
    }

    private static string GetEnclosingMethodName(
        SemanticModel semanticModel,
        SyntaxNode syntax,
        CancellationToken cancellationToken)
    {
        var methodSymbol =
            semanticModel.GetEnclosingSymbol(syntax.SpanStart, cancellationToken) as IMethodSymbol;
        return methodSymbol?.Name ?? "async method";
    }

    private static bool IsInsideAsyncMethod(
        SemanticModel semanticModel,
        SyntaxNode syntax,
        CancellationToken cancellationToken)
    {
        var methodSymbol =
            semanticModel.GetEnclosingSymbol(syntax.SpanStart, cancellationToken) as IMethodSymbol;
        return methodSymbol?.IsAsync == true;
    }
}
