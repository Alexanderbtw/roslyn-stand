using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RoslynStand.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private readonly static SymbolDisplayFormat FullyQualifiedTypeFormat =
        new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions:
            SymbolDisplayGenericsOptions.IncludeTypeParameters |
            SymbolDisplayGenericsOptions.IncludeVariance,
            miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource(
                "ValueObjectAttribute.g.cs",
                SourceText.From(
                    """
                    #nullable enable

                    [global::System.AttributeUsage(
                        global::System.AttributeTargets.Struct | global::System.AttributeTargets.Class,
                        AllowMultiple = false,
                        Inherited = false)]
                    public sealed class ValueObjectAttribute<T> : global::System.Attribute
                    {
                    }
                    """,
                    Encoding.UTF8));
        });

        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (syntaxContext, _) => TryCreateCandidate(syntaxContext))
            .Where(static candidate => candidate is not null);

        context.RegisterSourceOutput(candidates.Collect(), static (productionContext, collectedCandidates) =>
        {
            var seenHintNames = new HashSet<string>(global::System.StringComparer.Ordinal);

            foreach (Candidate candidate in collectedCandidates!)
            {
                if (candidate.Diagnostic is not null)
                {
                    productionContext.ReportDiagnostic(candidate.Diagnostic);
                    continue;
                }

                if (candidate.Target is null)
                {
                    continue;
                }

                if (!seenHintNames.Add(candidate.Target.HintName))
                {
                    continue;
                }

                productionContext.AddSource(
                    candidate.Target.HintName,
                    SourceText.From(GenerateSource(candidate.Target), Encoding.UTF8));
            }
        });
    }

    private static Candidate? TryCreateCandidate(GeneratorSyntaxContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        AttributeData? attribute = FindValueObjectAttribute(context.SemanticModel.GetDeclaredSymbol(declaration));
        if (attribute is null)
        {
            return null;
        }

        Location location = declaration.Identifier.GetLocation();

        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol typeSymbol)
        {
            return Candidate.CreateDiagnostic(
                Diagnostic.Create(
                    RoslynStandGeneratorDescriptors.UnsupportedDeclaration,
                    location,
                    declaration.Identifier.ValueText));
        }

        if (typeSymbol.ContainingType is not null || typeSymbol.TypeParameters.Length > 0)
        {
            return Candidate.CreateDiagnostic(
                Diagnostic.Create(
                    RoslynStandGeneratorDescriptors.UnsupportedDeclaration,
                    location,
                    typeSymbol.ToDisplayString()));
        }

        if (!IsSupportedShape(declaration))
        {
            return Candidate.CreateDiagnostic(
                Diagnostic.Create(
                    RoslynStandGeneratorDescriptors.InvalidTargetShape,
                    location,
                    typeSymbol.ToDisplayString()));
        }

        ITypeSymbol underlyingType = attribute.AttributeClass!.TypeArguments[0];
        if (!IsSupportedUnderlyingType(underlyingType))
        {
            return Candidate.CreateDiagnostic(
                Diagnostic.Create(
                    RoslynStandGeneratorDescriptors.UnsupportedUnderlyingType,
                    location,
                    underlyingType.ToDisplayString(FullyQualifiedTypeFormat)));
        }

        var target = new ValueObjectTarget(
            @namespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            accessibility: GetAccessibilityKeyword(typeSymbol),
            typeName: typeSymbol.Name,
            underlyingType: underlyingType.ToDisplayString(FullyQualifiedTypeFormat),
            hintName: $"{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "").Replace('.', '_')}.ValueObject.g.cs",
            requiresNullCheck: RequiresNullCheck(underlyingType),
            isString: underlyingType.SpecialType == SpecialType.System_String);

        return Candidate.CreateTarget(target);
    }

    private static AttributeData? FindValueObjectAttribute(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "ValueObjectAttribute", Arity: 1 } attributeClass)
            {
                continue;
            }

            if (!attributeClass.ContainingNamespace.IsGlobalNamespace)
            {
                continue;
            }

            return attribute;
        }

        return null;
    }

    private static bool IsSupportedShape(TypeDeclarationSyntax declaration)
    {
        if (declaration is not RecordDeclarationSyntax recordDeclaration)
        {
            return false;
        }

        return recordDeclaration.ClassOrStructKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StructKeyword) &&
               declaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)) &&
               declaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ReadOnlyKeyword));
    }

    private static bool IsSupportedUnderlyingType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String ||
            type.SpecialType == SpecialType.System_Boolean ||
            type.SpecialType == SpecialType.System_Char ||
            type.SpecialType == SpecialType.System_SByte ||
            type.SpecialType == SpecialType.System_Byte ||
            type.SpecialType == SpecialType.System_Int16 ||
            type.SpecialType == SpecialType.System_UInt16 ||
            type.SpecialType == SpecialType.System_Int32 ||
            type.SpecialType == SpecialType.System_UInt32 ||
            type.SpecialType == SpecialType.System_Int64 ||
            type.SpecialType == SpecialType.System_UInt64 ||
            type.SpecialType == SpecialType.System_Single ||
            type.SpecialType == SpecialType.System_Double ||
            type.SpecialType == SpecialType.System_Decimal)
        {
            return true;
        }

        return type is INamedTypeSymbol
        {
            Name: "Guid",
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
        };
    }

    private static bool RequiresNullCheck(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static string GetAccessibilityKeyword(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaredAccessibility switch
        {
            Accessibility.Internal => "internal",
            _ => "public"
        };
    }

    private static string GenerateSource(ValueObjectTarget target)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (target.Namespace is not null)
        {
            builder.Append("namespace ").Append(target.Namespace).AppendLine();
            builder.AppendLine("{");
        }

        string indentation = target.Namespace is null ? string.Empty : "    ";
        builder.Append(indentation).Append(target.Accessibility).Append(" readonly partial record struct ")
            .Append(target.TypeName).AppendLine();
        builder.Append(indentation).AppendLine("{");
        builder.Append(indentation).Append("    public ").Append(target.UnderlyingType).Append(" Value { get; }")
            .AppendLine();
        builder.AppendLine();
        builder.Append(indentation).Append("    private ").Append(target.TypeName).Append('(')
            .Append(target.UnderlyingType).Append(" value)").AppendLine();
        builder.Append(indentation).AppendLine("    {");

        if (target.RequiresNullCheck)
        {
            builder.Append(indentation)
                .AppendLine("        global::System.ArgumentNullException.ThrowIfNull(value);");
        }

        builder.Append(indentation).AppendLine("        Value = value;");
        builder.Append(indentation).AppendLine("    }");
        builder.AppendLine();
        builder.Append(indentation).Append("    public static ").Append(target.TypeName).Append(" Create(")
            .Append(target.UnderlyingType).Append(" value) => new(value);").AppendLine();
        builder.AppendLine();
        builder.Append(indentation).Append("    public override string ToString() => ")
            .Append(target.IsString ? "Value ?? string.Empty;" : "Value.ToString();").AppendLine();
        builder.Append(indentation).AppendLine("}");

        if (target.Namespace is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private sealed class Candidate
    {
        private Candidate(ValueObjectTarget? target, Diagnostic? diagnostic)
        {
            Target = target;
            Diagnostic = diagnostic;
        }

        public ValueObjectTarget? Target { get; }

        public Diagnostic? Diagnostic { get; }

        public static Candidate CreateDiagnostic(Diagnostic diagnostic) => new(null, diagnostic);

        public static Candidate CreateTarget(ValueObjectTarget target) => new(target, null);
    }

    private sealed class ValueObjectTarget
    {
        public ValueObjectTarget(
            string? @namespace,
            string accessibility,
            string typeName,
            string underlyingType,
            string hintName,
            bool requiresNullCheck,
            bool isString)
        {
            Namespace = @namespace;
            Accessibility = accessibility;
            TypeName = typeName;
            UnderlyingType = underlyingType;
            HintName = hintName;
            RequiresNullCheck = requiresNullCheck;
            IsString = isString;
        }

        public string? Namespace { get; }

        public string Accessibility { get; }

        public string TypeName { get; }

        public string UnderlyingType { get; }

        public string HintName { get; }

        public bool RequiresNullCheck { get; }

        public bool IsString { get; }
    }
}
