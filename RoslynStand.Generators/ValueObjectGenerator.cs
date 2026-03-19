using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RoslynStand.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource(
                "ValueObjectAttribute.g.cs",
                SourceText.From(ValueObjectGeneratorBackend.AttributeSource, Encoding.UTF8));
        });

        IncrementalValuesProvider<ValueObjectGeneratorBackend.Candidate?> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => ValueObjectGeneratorBackend.TryCreateCandidate(syntaxContext))
            .Where(static candidate => candidate is not null);

        context.RegisterSourceOutput(
            candidates.Collect(),
            static (productionContext, collectedCandidates) =>
            {
                var seenHintNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (ValueObjectGeneratorBackend.Candidate candidate in collectedCandidates!)
                {
                    if (candidate?.Diagnostic is not null)
                    {
                        productionContext.ReportDiagnostic(candidate.Diagnostic);
                        continue;
                    }

                    if (candidate?.Target is null)
                    {
                        continue;
                    }

                    if (!seenHintNames.Add(candidate.Target.HintName))
                    {
                        continue;
                    }

                    productionContext.AddSource(
                        candidate.Target.HintName,
                        SourceText.From(
                            ValueObjectGeneratorBackend.GenerateSource(candidate.Target),
                            Encoding.UTF8));
                }
            });
    }
}
