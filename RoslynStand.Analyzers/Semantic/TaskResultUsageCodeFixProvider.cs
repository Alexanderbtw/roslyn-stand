using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynStand.Analyzers.Semantic;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskResultUsageCodeFixProvider))]
[Shared]
public sealed class TaskResultUsageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RoslynStandDescriptors.TaskResultUsage.Id];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public async override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);
        context.RegisterCodeFix(
            CodeAction.Create(
                "Use await instead of Task.Result",
                _ => RewriteBlockingCallAsync(context.Document, root, node),
                "UseAwait"),
            context.Diagnostics);
    }

    private static AwaitExpressionSyntax CreateAwaitExpression(
        ExpressionSyntax expression,
        SyntaxNode triviaSource) => SyntaxFactory
        .AwaitExpression(ParenthesizeIfNeeded(expression.WithoutTrivia()))
        .WithTriviaFrom(triviaSource);

    private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax or InvocationExpressionSyntax or MemberAccessExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression);

    private static Task<Document> RewriteBlockingCallAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node)
    {
        SyntaxNode? replacementRoot = node is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Result"
        } memberAccess
            ? root.ReplaceNode(memberAccess, CreateAwaitExpression(memberAccess.Expression, memberAccess))
            : null;

        return Task.FromResult(
            replacementRoot is null
                ? document
                : document.WithSyntaxRoot(replacementRoot));
    }
}
