using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RoslynStand.Generators;

namespace RoslynStand.Generators.Tests;

internal static class GeneratorTestHarness
{
    public static GeneratorRunResult RunGenerator(params string[] sources)
    {
        CSharpCompilation compilation = CreateCompilation(sources);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new ValueObjectGenerator().AsSourceGenerator()],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> generatorDriverDiagnostics);

        GeneratorDriverRunResult runResult = driver.GetRunResult();

        return new GeneratorRunResult(
            outputCompilation,
            generatorDriverDiagnostics,
            outputCompilation.GetDiagnostics(),
            runResult.Results.SelectMany(static result => result.GeneratedSources).ToImmutableArray(),
            runResult.Results.SelectMany(static result => result.Diagnostics).ToImmutableArray());
    }

    public static GeneratorRunResult RunGeneratorOnSample()
    {
        string sampleDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../RoslynStand.Sample"));

        string[] sources = Directory
            .EnumerateFiles(sampleDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToArray();

        return RunGenerator(sources);
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<string> sources)
    {
        SyntaxTree[] syntaxTrees = sources
            .Select(static source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees,
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        IEnumerable<PortableExecutableReference>? trustedPlatformAssemblies =
            ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));

        if (trustedPlatformAssemblies is null)
        {
            return [MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location)];
        }

        return trustedPlatformAssemblies.Cast<MetadataReference>().ToImmutableArray();
    }
}

internal sealed record GeneratorRunResult(
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> DriverDiagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources,
    ImmutableArray<Diagnostic> GeneratorDiagnostics);
