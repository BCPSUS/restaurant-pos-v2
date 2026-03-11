using System.Collections.Generic;

namespace RestaurantPOS.Domain.Entities;

public sealed class TableEntity
{
    public long Id { get; set; }

    // e.g. "1", "2", "A1", "Patio-3"
    public string TableNo { get; set; } = "";

    public bool IsActive { get; set; } = true;

    // navigation
    public List<Order> Orders { get; set; } = new();
}