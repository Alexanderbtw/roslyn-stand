using System.Threading;
using System.Threading.Tasks;

namespace RoslynStand.Sample;

public sealed class OrderService(IOrderRepository repository)
{
    public async Task<OrderDto?> GetOrder(
        OrderId orderId,
        CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(orderId, cancellationToken);

    public Task<OrderDto?> LoadOrderAsync(
        OrderId orderId) =>
        repository.GetByIdAsync(orderId, CancellationToken.None);

    public async Task<OrderSummaryDto> GetSummaryAsync(
        OrderId orderId,
        CancellationToken cancellationToken)
    {
        OrderDto? order = repository.GetByIdAsync(orderId, cancellationToken).Result;
        return await Task.FromResult(new OrderSummaryDto(OrderId.Create(orderId.Value), order?.Total ?? Price.Create(0m)));
    }
}
