using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Infrastructure.Database;

namespace RestaurantPOS.Infrastructure.Repositories;

public sealed class MenuRepository : IMenuRepository
{
    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public MenuRepository(IDbContextFactory<PosDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // =========================
    // Category
    // =========================

    public async Task<List<MenuCategory>> GetCategoriesAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.MenuCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<MenuCategory?> GetCategoryByIdAsync(long categoryId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.MenuCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId, ct);
    }

    public async Task<int> GetNextCategorySortOrderAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var maxSort = await db.MenuCategories
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        return (maxSort ?? 0) + 1;
    }

    public async Task<MenuCategory> AddCategoryAsync(MenuCategory category, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.MenuCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return category;
    }

    public async Task<MenuCategory> UpdateCategoryAsync(MenuCategory category, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var dbCategory = await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == category.Id, ct);
        if (dbCategory is null)
            throw new InvalidOperationException("Menu category not found.");

        dbCategory.Name = category.Name;
        dbCategory.SortOrder = category.SortOrder;
        dbCategory.IsActive = category.IsActive;

        await db.SaveChangesAsync(ct);
        return dbCategory;
    }

    public async Task DeleteCategoryAsync(long categoryId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var dbCategory = await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == categoryId, ct);
        if (dbCategory is null)
            throw new InvalidOperationException("Menu category not found.");

        bool hasItems = await db.MenuItems.AnyAsync(x => x.CategoryId == categoryId, ct);
        if (hasItems)
            throw new InvalidOperationException("Cannot delete category because it still has items.");

        db.MenuCategories.Remove(dbCategory);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> CategoryHasItemsAsync(long categoryId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.MenuItems.AnyAsync(x => x.CategoryId == categoryId, ct);
    }

    // =========================
    // Item
    // =========================

    public async Task<List<MenuItem>> GetItemsByCategoryAsync(long categoryId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.MenuItems
            .AsNoTracking()
            .Where(i => i.IsActive && i.CategoryId == categoryId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);
    }

    public async Task<List<MenuItem>> GetAllItemsByCategoryAsync(long categoryId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.MenuItems
            .AsNoTracking()
            .Where(i => i.CategoryId == categoryId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);
    }

    public async Task<MenuItem?> GetItemByIdAsync(long itemId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.MenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId, ct);
    }

    public async Task<int> GetNextSortOrderAsync(long categoryId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var maxSort = await db.MenuItems
            .Where(x => x.CategoryId == categoryId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(ct);

        return (maxSort ?? 0) + 1;
    }

    public async Task<MenuItem> AddItemAsync(MenuItem item, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(ct);

        return item;
    }

    public async Task<MenuItem> UpdateItemAsync(MenuItem item, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var dbItem = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
        if (dbItem is null)
            throw new InvalidOperationException("Menu item not found.");

        dbItem.CategoryId = item.CategoryId;
        dbItem.ItemNo = item.ItemNo;
        dbItem.Name = item.Name;
        dbItem.Name2 = item.Name2;
        dbItem.Price = item.Price;
        dbItem.Station = item.Station;
        dbItem.SortOrder = item.SortOrder;
        dbItem.IsActive = item.IsActive;

        await db.SaveChangesAsync(ct);
        return dbItem;
    }

    public async Task DeleteItemAsync(long itemId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var dbItem = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (dbItem is null)
            throw new InvalidOperationException("Menu item not found.");

        db.MenuItems.Remove(dbItem);
        await db.SaveChangesAsync(ct);
    }
}