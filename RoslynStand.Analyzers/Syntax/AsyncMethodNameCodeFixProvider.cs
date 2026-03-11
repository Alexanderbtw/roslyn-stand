using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace RoslynStand.Analyzers.Syntax;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncMethodNameCodeFixProvider))]
[Shared]
public sealed class AsyncMethodNameCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RoslynStandDescriptors.AsyncMethodName.Id];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public async override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        var method = root?.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Rename method to end with Async",
                cancellationToken => RenameAsync(context.Document, method, cancellationToken),
                "RenameAsyncMethod"),
            context.Diagnostics);
    }

    private static async Task<Solution> RenameAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel =
            await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document.Project.Solution;
        }

        ISymbol? symbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
        if (symbol is null)
        {
            return document.Project.Solution;
        }

        string newName = symbol.Name.EndsWith("Async", StringComparison.Ordinal)
            ? symbol.Name
            : symbol.Name + "Async";

        return await Renamer.RenameSymbolAsync(
            document.Project.Solution,
            symbol,
            new SymbolRenameOptions(),
            newName,
            cancellationToken).ConfigureAwait(false);
    }
}
