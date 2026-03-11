using RestaurantPOS.Domain.Entities;

namespace RestaurantPOS.Application.Interfaces;

public interface IMenuRepository
{
    // =========================
    // Category
    // =========================
    Task<List<MenuCategory>> GetCategoriesAsync(CancellationToken ct);

    Task<MenuCategory?> GetCategoryByIdAsync(long categoryId, CancellationToken ct);

    Task<int> GetNextCategorySortOrderAsync(CancellationToken ct);

    Task<MenuCategory> AddCategoryAsync(MenuCategory category, CancellationToken ct);

    Task<MenuCategory> UpdateCategoryAsync(MenuCategory category, CancellationToken ct);

    Task DeleteCategoryAsync(long categoryId, CancellationToken ct);

    Task<bool> CategoryHasItemsAsync(long categoryId, CancellationToken ct);

    // =========================
    // Item
    // =========================
    Task<List<MenuItem>> GetItemsByCategoryAsync(long categoryId, CancellationToken ct);

    Task<List<MenuItem>> GetAllItemsByCategoryAsync(long categoryId, CancellationToken ct);

    Task<MenuItem?> GetItemByIdAsync(long itemId, CancellationToken ct);

    Task<int> GetNextSortOrderAsync(long categoryId, CancellationToken ct);

    Task<MenuItem> AddItemAsync(MenuItem item, CancellationToken ct);

    Task<MenuItem> UpdateItemAsync(MenuItem item, CancellationToken ct);

    Task DeleteItemAsync(long itemId, CancellationToken ct);
}