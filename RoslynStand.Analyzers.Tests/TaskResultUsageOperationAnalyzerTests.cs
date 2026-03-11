using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using RoslynStand.Analyzers.Semantic;

namespace RoslynStand.Analyzers.Tests;

public sealed class TaskResultUsageOperationAnalyzerTests
{
    [Fact]
    public async Task Does_not_report_task_result_outside_async_method()
    {
        const string source = """
                              using System.Threading.Tasks;

                              public sealed class OrderService
                              {
                                  public Task<int> GetSummary()
                                  {
                                      var value = Task.FromResult(42).Result;
                                      return Task.FromResult(value);
                                  }
                              }
                              """;

        ImmutableArray<Diagnostic> diagnostics =
            await RoslynTestHarness.GetDiagnosticsAsync(source, new TaskResultUsageOperationAnalyzer());

        Assert.Empty(diagnostics);
    }
    [Fact]
    public async Task Reports_task_result_inside_async_method()
    {
        const string source = """
                              using System.Threading.Tasks;

                              public sealed class OrderService
                              {
                                  public async Task<int> GetSummaryAsync()
                                  {
                                      var value = Task.FromResult(42).Result;
                                      return await Task.FromResult(value);
                                  }
                              }
                              """;

        ImmutableArray<Diagnostic> diagnostics =
            await RoslynTestHarness.GetDiagnosticsAsync(source, new TaskResultUsageOperationAnalyzer());

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RSR003", diagnostic.Id);
    }

    [Fact]
    public async Task Rewrites_result_to_await()
    {
        const string source = """
                              using System.Threading.Tasks;

                              public sealed class OrderService
                              {
                                  public async Task<int> GetSummaryAsync()
                                  {
                                      var value = Task.FromResult(42).Result;
                                      return await Task.FromResult(value);
                                  }
                              }
                              """;

        string updated = await RoslynTestHarness.ApplyCodeFixAsync(
            source,
            new TaskResultUsageOperationAnalyzer(),
            new TaskResultUsageCodeFixProvider(),
            "RSR003");

        Assert.Contains("var value = await Task.FromResult(42);", updated);
    }
}
