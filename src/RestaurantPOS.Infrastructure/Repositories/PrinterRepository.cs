using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;
using RestaurantPOS.Infrastructure.Database;

namespace RestaurantPOS.Infrastructure.Repositories;

public sealed class PrinterRepository : IPrinterRepository
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public PrinterRepository(IDbContextFactory<PosDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Printer?> GetByStationAsync(StationType station, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.Printers
            .AsNoTracking()
            .Where(x => x.IsEnabled && x.Station == station)
            .OrderBy(x => x.SortOrder)
            .FirstOrDefaultAsync(ct);
    }
}