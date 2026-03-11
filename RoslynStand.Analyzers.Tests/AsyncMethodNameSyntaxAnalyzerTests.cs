using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using RoslynStand.Analyzers.Syntax;

namespace RoslynStand.Analyzers.Tests;

public sealed class AsyncMethodNameSyntaxAnalyzerTests
{
    [Fact]
    public async Task Renames_method_and_call_sites()
    {
        const string source = """
                              using System.Threading.Tasks;

                              public sealed class OrderService
                              {
                                  public async Task<int> GetOrder()
                                  {
                                      return await Task.FromResult(42);
                                  }

                                  public async Task<int> ForwardAsync()
                                  {
                                      return await GetOrder();
                                  }
                              }
                              """;

        string updated = await RoslynTestHarness.ApplyCodeFixAsync(
            source,
            new AsyncMethodNameSyntaxAnalyzer(),
            new AsyncMethodNameCodeFixProvider(),
            "RSR001");

        Assert.Contains("GetOrderAsync()", updated);
        Assert.DoesNotContain("await GetOrder();", updated);
    }
    [Fact]
    public async Task Reports_async_method_without_async_suffix()
    {
        const string source = """
                              using System.Threading.Tasks;

                              public sealed class OrderService
                              {
                                  public async Task<int> GetOrder()
                                  {
                                      return await Task.FromResult(42);
                                  }
                              }
                              """;

        ImmutableArray<Diagnostic> diagnostics =
            await RoslynTestHarness.GetDiagnosticsAsync(source, new AsyncMethodNameSyntaxAnalyzer());

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RSR001", diagnostic.Id);
    }
}
