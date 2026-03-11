namespace RestaurantPOS.Domain.Enums;

public enum OrderStatus
{
    Created = 1,
    Editing = 2,
    SentToKitchen = 3,
    Paid = 4,
    Closed = 5,
    Refunded = 6,
    Cancelled = 7
}