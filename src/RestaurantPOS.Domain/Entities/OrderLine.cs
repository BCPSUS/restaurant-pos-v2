public class OrderLine
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    public long MenuItemId { get; set; }

    public string ItemNoSnapshot { get; set; } = "";
    public string NameSnapshot { get; set; } = "";
    public string? Name2Snapshot { get; set; }
    public string? StationSnapshot { get; set; }

    public decimal Price { get; set; }
    public int Qty { get; set; }
    public decimal Total { get; set; }

    public bool IsSentToKitchen { get; set; }
    public DateTime? SentToKitchenAt { get; set; }

    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }

    public bool VoidSentToKitchen { get; set; }
    public DateTime? VoidSentToKitchenAt { get; set; }

    public string? VoidReason { get; set; }
}