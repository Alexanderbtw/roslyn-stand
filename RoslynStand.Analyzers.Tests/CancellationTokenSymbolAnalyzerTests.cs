using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using RoslynStand.Analyzers.Symbol;

namespace RoslynStand.Analyzers.Tests;

public sealed class CancellationTokenSymbolAnalyzerTests
{
    [Fact]
    public async Task Adds_parameter_and_replaces_cancellation_token_none()
    {
        const string source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed record OrderDto(string Id);

                              public interface IOrderRepository
                              {
                                  Task<OrderDto?> GetByIdAsync(string orderId, CancellationToken cancellationToken);
                              }

                              public sealed class OrderService(IOrderRepository repository)
                              {
                                  public Task<OrderDto?> LoadOrderAsync(string orderId)
                                  {
                                      return repository.GetByIdAsync(orderId, CancellationToken.None);
                                  }
                              }
                              """;

        string updated = await RoslynTestHarness.ApplyCodeFixAsync(
            source,
            new CancellationTokenSymbolAnalyzer(),
            new CancellationTokenCodeFixProvider(),
            "RSR002");

        Assert.Contains("LoadOrderAsync(string orderId, CancellationToken cancellationToken)", updated);
        Assert.Contains("GetByIdAsync(orderId, cancellationToken)", updated);
    }
    [Fact]
    public async Task Reports_public_async_method_without_cancellation_token()
    {
        const string source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed record OrderDto(string Id);

                              public interface IOrderRepository
                              {
                                  Task<OrderDto?> GetByIdAsync(string orderId, CancellationToken cancellationToken);
                              }

                              public sealed class OrderService(IOrderRepository repository)
                              {
                                  public Task<OrderDto?> LoadOrderAsync(string orderId)
                                  {
                                      return repository.GetByIdAsync(orderId, CancellationToken.None);
                                  }
                              }
                              """;

        ImmutableArray<Diagnostic> diagnostics =
            await RoslynTestHarness.GetDiagnosticsAsync(source, new CancellationTokenSymbolAnalyzer());

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("RSR002", diagnostic.Id);
    }
}
