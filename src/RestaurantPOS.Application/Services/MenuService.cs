using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;

namespace RestaurantPOS.Application.Services;

public sealed class MenuService
{
    private readonly IMenuRepository _repo;

    public MenuService(IMenuRepository repo)
    {
        _repo = repo;
    }

    public Task<List<MenuCategory>> GetCategoriesAsync(CancellationToken ct)
        => _repo.GetCategoriesAsync(ct);

    public Task<List<MenuItem>> GetItemsByCategoryAsync(long categoryId, CancellationToken ct)
        => _repo.GetItemsByCategoryAsync(categoryId, ct);
}