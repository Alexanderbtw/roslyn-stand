using Microsoft.CodeAnalysis;

namespace RoslynStand.Generators;

internal static class RoslynStandGeneratorDescriptors
{
    public readonly static DiagnosticDescriptor InvalidTargetShape = new(
        id: "RSG001",
        title: "ValueObject<T> requires a readonly partial record struct",
        messageFormat:
        "ValueObject<T> can only be applied to 'readonly partial record struct' declarations. '{0}' is not supported.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor UnsupportedUnderlyingType = new(
        id: "RSG002",
        title: "Unsupported ValueObject<T> underlying type",
        messageFormat:
        "Underlying type '{0}' is not supported by ValueObject<T>. Allowed types are string, Guid, bool, char, integral types, floating-point types, and decimal.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor UnsupportedDeclaration = new(
        id: "RSG003",
        title: "Unsupported ValueObject<T> declaration",
        messageFormat:
        "ValueObject<T> only supports non-nested, non-generic type declarations. '{0}' is not supported.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
