using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynStand.Analyzers.Symbol;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CancellationTokenCodeFixProvider))]
[Shared]
public sealed class CancellationTokenCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RoslynStandDescriptors.MissingCancellationToken.Id];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public async override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var method = root.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add CancellationToken parameter",
                _ => AddCancellationTokenAsync(context.Document, root, method),
                "AddCancellationToken"),
            context.Diagnostics);
    }

    private static Task<Document> AddCancellationTokenAsync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax method)
    {
        ParameterSyntax parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
            .WithType(SyntaxFactory.IdentifierName("CancellationToken"));

        MethodDeclarationSyntax updatedMethod = method.AddParameterListParameters(parameter);
        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Where(IsCancellationTokenNone),
            static (original, _) =>
                SyntaxFactory.IdentifierName("cancellationToken").WithTriviaFrom(original));

        SyntaxNode updatedRoot = root.ReplaceNode(method, updatedMethod);
        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static bool IsCancellationTokenNone(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "CancellationToken" } &&
        memberAccess.Name.Identifier.ValueText == "None";
}
