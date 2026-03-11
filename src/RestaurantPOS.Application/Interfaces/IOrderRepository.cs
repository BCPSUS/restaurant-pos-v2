using RestaurantPOS.Domain.Entities;

namespace RestaurantPOS.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(long id, CancellationToken ct);
    Task<long> CreateAsync(Order order, CancellationToken ct);
    Task SaveAsync(Order order, CancellationToken ct);

    // ✅ Table lookup (default interface method to avoid breaking build)
    Task<TableEntity?> FindTableByNoAsync(string tableNo, CancellationToken ct)
        => throw new NotImplementedException("FindTableByNoAsync is not implemented yet.");
}
