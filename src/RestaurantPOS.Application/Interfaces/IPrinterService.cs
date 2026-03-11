using RestaurantPOS.Domain.Entities;

namespace RestaurantPOS.Application.Interfaces;

public interface IPrinterService
{
    Task PrintReceiptAsync(Order order, CancellationToken ct);
    Task PrintKitchenAsync(Order order, CancellationToken ct);
}
