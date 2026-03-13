using System;

namespace RoslynStand.Sample;

public sealed record OrderDto(OrderId Id, Price Total, DateTime OrderDate, CustomerId CustomerId);