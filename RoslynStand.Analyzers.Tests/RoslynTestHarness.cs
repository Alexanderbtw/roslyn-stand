using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace RoslynStand.Analyzers.Tests;

internal static class RoslynTestHarness
{
    public static async Task<string> ApplyCodeFixAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider codeFixProvider,
        string diagnosticId)
    {
        using AdhocWorkspace workspace = CreateWorkspace();
        Document document = CreateDocument(workspace, source);
        Diagnostic diagnostic = (await GetDiagnosticsAsync(document, analyzer))
            .Single(item => item.Id == diagnosticId);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await codeFixProvider.RegisterCodeFixesAsync(context);
        Assert.NotEmpty(actions);

        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None);
        foreach (CodeActionOperation operation in operations)
        {
            operation.Apply(workspace, CancellationToken.None);
        }

        Document? changedDocument = workspace.CurrentSolution.GetDocument(document.Id);
        Assert.NotNull(changedDocument);

        SourceText text = await changedDocument!.GetTextAsync(CancellationToken.None);
        return text.ToString();
    }
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        DiagnosticAnalyzer analyzer)
    {
        using AdhocWorkspace workspace = CreateWorkspace();
        Document document = CreateDocument(workspace, source);
        Compilation? compilation = await document.Project.GetCompilationAsync(CancellationToken.None);

        if (compilation is null)
        {
            return [];
        }

        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        return diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToImmutableArray();
    }

    private static Document CreateDocument(AdhocWorkspace workspace, string source)
    {
        var projectId = ProjectId.CreateNewId();

        Solution solution = workspace.CurrentSolution
            .AddProject(
                ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    "TestProject",
                    "TestProject",
                    LanguageNames.CSharp,
                    parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
                    compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
            .WithProjectMetadataReferences(projectId, GetMetadataReferences());

        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, "Test.cs", SourceText.From(source));

        Document? document = solution.GetDocument(documentId);
        Assert.NotNull(document);
        return document!;
    }

    private static AdhocWorkspace CreateWorkspace()
    {
        var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        return new AdhocWorkspace(host);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        Document document,
        DiagnosticAnalyzer analyzer)
    {
        Compilation? compilation = await document.Project.GetCompilationAsync(CancellationToken.None);
        Assert.NotNull(compilation);

        ImmutableArray<Diagnostic> diagnostics = await compilation!
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);

        return diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        IEnumerable<PortableExecutableReference>? trustedPlatformAssemblies =
            ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator)
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));

        if (trustedPlatformAssemblies is null)
        {
            return [MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location)];
        }

        return trustedPlatformAssemblies.Cast<MetadataReference>().ToImmutableArray();
    }
}
