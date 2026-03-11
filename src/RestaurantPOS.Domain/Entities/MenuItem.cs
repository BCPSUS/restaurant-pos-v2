namespace RestaurantPOS.Domain.Entities;

public class MenuItem
{
    public long Id { get; set; }

    // Item number (used for import/export or POS lookup)
    public string ItemNo { get; set; } = "";

    // Primary display name
    public string Name { get; set; } = "";

    // Secondary language (optional)
    public string? Name2 { get; set; }

    // Price
    public decimal Price { get; set; }

    // Station routing (Sushi / Kitchen / Bar)
    public string Station { get; set; } = "Kitchen";

    // Category
    public long CategoryId { get; set; }

    public MenuCategory Category { get; set; } = null!;

    // Optional printer override (future use)
    public long? PrinterId { get; set; }

    // Manual sorting
    public int SortOrder { get; set; }

    // Active flag
    public bool IsActive { get; set; } = true;
}