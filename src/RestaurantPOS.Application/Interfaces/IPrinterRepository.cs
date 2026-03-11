using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;

namespace RestaurantPOS.Application.Interfaces;

public interface IPrinterRepository
{
    Task<Printer?> GetByStationAsync(StationType station, CancellationToken ct = default);
}