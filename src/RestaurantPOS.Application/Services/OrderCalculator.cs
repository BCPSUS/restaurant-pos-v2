using RestaurantPOS.Domain.Entities;

namespace RestaurantPOS.Application.Services;

public sealed class OrderCalculator
{
    public void Recalculate(Order order, decimal taxRate)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));

        // 1) Compute each line total (even voided lines keep Total for display)
        foreach (var l in order.Lines)
        {
            l.Total = Math.Round(l.Price * l.Qty, 2, MidpointRounding.AwayFromZero);
        }

        // 2) Subtotal excludes voided lines
        var subtotal = order.Lines
            .Where(l => !l.IsVoided)
            .Sum(l => l.Total);

        subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);

        var tax = Math.Round(subtotal * taxRate, 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(subtotal + tax, 2, MidpointRounding.AwayFromZero);

        order.Subtotal = subtotal;
        order.Tax = tax;
        order.Total = total;
    }
}