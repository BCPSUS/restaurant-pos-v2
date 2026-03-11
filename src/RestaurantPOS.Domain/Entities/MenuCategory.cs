namespace RestaurantPOS.Domain.Entities;

public class MenuCategory
{
    public long Id { get; set; }

    // Primary display name
    public string Name { get; set; } = "";

    // Optional secondary language
    public string? Name2 { get; set; }

    // Manual sorting order
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}