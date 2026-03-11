using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;

public class Order
{
    public long Id { get; set; }

    public string OrderNumber { get; set; } = "";

    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Editing;

    public long? StaffId { get; set; }
    public long? CustomerId { get; set; }

    public long? TableId { get; set; }
    public string? TableNoSnapshot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public DateTime BusinessDate { get; set; } = DateTime.UtcNow.Date;

    public DateTime? KitchenPrevSentAt { get; set; }
    public DateTime? KitchenLastSentAt { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.None;
    public DateTime? PaidAt { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    public List<OrderLine> Lines { get; set; } = new();
}