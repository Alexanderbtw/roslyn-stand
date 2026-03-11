using Microsoft.CodeAnalysis;

namespace RoslynStand.Analyzers;

internal static class RoslynStandDescriptors
{
    public readonly static DiagnosticDescriptor AsyncMethodName = new(
        id: "RSR001",
        title: "Async methods should end with Async",
        messageFormat: "Async method '{0}' should end with 'Async'",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor MissingCancellationToken = new(
        id: "RSR002",
        title: "Public async APIs should accept CancellationToken",
        messageFormat: "Public async-returning method '{0}' should accept a CancellationToken parameter",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor TaskResultUsage = new(
        id: "RSR003",
        title: "Do not use Task.Result inside async methods",
        messageFormat: "Replace Task.Result with await in async method '{0}'",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
