using System;

namespace RoslynStand.Sample;

[ValueObject<string>]
public readonly partial record struct OrderId;

[ValueObject<Guid>]
public readonly partial record struct CustomerId;

[ValueObject<decimal>]
public readonly partial record struct Price;

