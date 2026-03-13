using System.Threading;
using System.Threading.Tasks;

namespace RoslynStand.Sample;

public interface IOrderRepository
{
    Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken);
}
