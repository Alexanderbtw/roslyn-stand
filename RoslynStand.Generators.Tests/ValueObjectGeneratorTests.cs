using System.Linq;

using Microsoft.CodeAnalysis;

namespace RoslynStand.Generators.Tests;

public sealed class ValueObjectGeneratorTests
{
    [Fact]
    public void Generates_string_wrapper_and_compiles()
    {
        const string source = """
                              namespace Demo;

                              [ValueObject<string>]
                              public readonly partial record struct OrderId;

                              public static class Usage
                              {
                                  public static string Render() => OrderId.Create("42").Value;
                              }
                              """;

        GeneratorRunResult result = GeneratorTestHarness.RunGenerator(source);

        Assert.Empty(result.DriverDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        GeneratedSourceResult generatedValueObject = Assert.Single(
            result.GeneratedSources.Where(static source => source.HintName != "ValueObjectAttribute.g.cs"));

        string generatedSource = generatedValueObject.SourceText.ToString();
        Assert.Contains("public global::System.String Value { get; }", generatedSource);
        Assert.Contains("public static OrderId Create(global::System.String value) => new(value);", generatedSource);
        Assert.Contains("global::System.ArgumentNullException.ThrowIfNull(value);", generatedSource);
    }

    [Fact]
    public void Generates_guid_and_int_wrappers()
    {
        const string source = """
                              namespace Demo;

                              [ValueObject<System.Guid>]
                              public readonly partial record struct OrderGuid;

                              [ValueObject<int>]
                              public readonly partial record struct OrderNumber;

                              public static class Usage
                              {
                                  public static string Render(System.Guid value)
                                  {
                                      OrderGuid guid = OrderGuid.Create(value);
                                      OrderNumber number = OrderNumber.Create(42);
                                      return $"{guid}-{number.Value}";
                                  }
                              }
                              """;

        GeneratorRunResult result = GeneratorTestHarness.RunGenerator(source);

        Assert.Empty(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Equal(2, result.GeneratedSources.Count(static item => item.HintName != "ValueObjectAttribute.g.cs"));

        Assert.DoesNotContain(
            "ArgumentNullException.ThrowIfNull",
            result.GeneratedSources.Single(static item => item.HintName.Contains("OrderNumber")).SourceText.ToString());
    }

    [Fact]
    public void Reports_invalid_target_shape()
    {
        const string source = """
                              [ValueObject<string>]
                              public partial struct OrderId
                              {
                              }
                              """;

        GeneratorRunResult result = GeneratorTestHarness.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("RSG001", diagnostic.Id);
    }

    [Fact]
    public void Reports_unsupported_underlying_type()
    {
        const string source = """
                              [ValueObject<System.DateTime>]
                              public readonly partial record struct CreatedAt;
                              """;

        GeneratorRunResult result = GeneratorTestHarness.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("RSG002", diagnostic.Id);
    }

    [Fact]
    public void Reports_unsupported_nested_declaration()
    {
        const string source = """
                              public static class Container
                              {
                                  [ValueObject<string>]
                                  public readonly partial record struct OrderId;
                              }
                              """;

        GeneratorRunResult result = GeneratorTestHarness.RunGenerator(source);

        Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("RSG003", diagnostic.Id);
    }

    [Fact]
    public void Sample_compiles_with_generated_order_id()
    {
        GeneratorRunResult result = GeneratorTestHarness.RunGeneratorOnSample();

        Assert.Empty(result.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.CompilationDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(result.GeneratedSources, static source => source.HintName.Contains("OrderId"));
    }
}
