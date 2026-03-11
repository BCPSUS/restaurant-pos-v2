using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Infrastructure.Database;

namespace RestaurantPOS.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public OrderRepository(IDbContextFactory<PosDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<long> CreateAsync(Order order, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return order.Id;
    }

    public async Task<Order?> GetByIdAsync(long orderId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.Orders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.Orders.Update(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TableEntity?> FindTableByNoAsync(string tableNo, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.Tables
            .FirstOrDefaultAsync(x => x.TableNo == tableNo, ct);
    }
}