using System.Threading;
using System.Threading.Tasks;

namespace RoslynStand.Sample;

public sealed class OrderService(IOrderRepository repository)
{
    public async Task<OrderDto?> GetOrder(
        string orderId,
        CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(orderId, cancellationToken);

    public Task<OrderDto?> LoadOrderAsync(
        string orderId) =>
        repository.GetByIdAsync(orderId, CancellationToken.None);

    public async Task<OrderSummaryDto> GetSummaryAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        OrderDto? order = repository.GetByIdAsync(orderId, cancellationToken).Result;
        return await Task.FromResult(new OrderSummaryDto(orderId, order?.Total ?? 0m));
    }
}
