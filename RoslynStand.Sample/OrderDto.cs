namespace RoslynStand.Sample;

public sealed record OrderDto(string Id, decimal Total);

public sealed record OrderSummaryDto(string Id, decimal Total);
