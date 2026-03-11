using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;
using RestaurantPOS.Shared;

namespace RestaurantPOS.Application.Services;

public sealed class OrderService
{
    private readonly IOrderRepository _repo;
    private readonly IPrinterService _printer;
    private readonly OrderCalculator _calc;
    private readonly IMenuRepository _menuRepository;

    public decimal TaxRate { get; set; } = 0.07m;

    public OrderService(IOrderRepository repo, IPrinterService printer, OrderCalculator calc, IMenuRepository menuRepository)
    {
        _repo = repo;
        _printer = printer;
        _calc = calc;
        _menuRepository = menuRepository;
    }

    // ✅ Read for UI
    public async Task<Order?> GetAsync(long orderId, CancellationToken ct)
    {
        return await _repo.GetByIdAsync(orderId, ct);
    }

    // ✅ Create new order with readable order number
    public async Task<Result<long>> CreateAsync(OrderType type, long? staffId, string? tableNo, CancellationToken ct)
    {
        var now = DateTime.Now; // local time for human-readable order number
        var orderNo = $"{now:yyMMdd}-{now:HHmmss}"; // e.g. 260303-142530

        long? tableId = null;
        string? tableNoSnapshot = null;

        if (type == OrderType.DineIn)
        {
            if (string.IsNullOrWhiteSpace(tableNo))
                return Result<long>.Fail("DineIn requires Table #.");

            var t = await _repo.FindTableByNoAsync(tableNo.Trim(), ct);
            if (t is null)
                return Result<long>.Fail($"Table '{tableNo}' not found.");

            tableId = t.Id;
            tableNoSnapshot = t.TableNo;
        }

        var order = new Order
        {
            OrderType = type,
            Status = OrderStatus.Editing,
            StaffId = staffId,
            TableId = tableId,
            TableNoSnapshot = tableNoSnapshot,
            CreatedAt = DateTime.UtcNow,
            BusinessDate = DateTime.UtcNow.Date,
            PaymentMethod = PaymentMethod.None,
            PaidAt = null
        };

        var id = await _repo.CreateAsync(order, ct);
        return Result<long>.Ok(id);
    }

    

    // ✅ Add item (later will load menu data from DB)
    public async Task<Result> AddItemAsync(long orderId, long menuItemId, string nameSnapshot, decimal price, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Fail("Order not found.");
        var menuItem = await _menuRepository.GetItemByIdAsync(menuItemId, ct);
        if (menuItem is null) return Result.Fail("Menu item not found.");

        // ✅ If Paid/Closed/Cancelled etc. still block
        if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Closed || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refunded)
            return Result.Fail($"Order is {order.Status}. Cannot add items.");

        // ✅ NEW: If order was SentToKitchen and user adds items, implicitly reopen for adding
        if (order.Status == OrderStatus.SentToKitchen)
        {
            order.Status = OrderStatus.Editing;
        }

        // ✅ Merge rule:
        // Only merge into a line that is NOT sent to kitchen and NOT voided.
        // If earlier same item was sent to kitchen, we DO NOT merge; we create a new line.
        var existing = order.Lines.FirstOrDefault(l =>
            !l.IsVoided &&
            !l.IsSentToKitchen &&
            l.MenuItemId == menuItemId &&
            l.Price == menuItem.Price);

        if (existing is not null)
        {
            existing.Qty += 1;
        }
        else
        {
            order.Lines.Add(new OrderLine
            {
                OrderId = order.Id,
                MenuItemId = menuItemId,

                ItemNoSnapshot = menuItem.ItemNo ?? "",
                NameSnapshot = menuItem.Name,
                Name2Snapshot = menuItem.Name2,
                StationSnapshot = menuItem.Station,

                Price = menuItem.Price,
                Qty = 1,
                IsVoided = false,
                IsSentToKitchen = false
            });
        }

        _calc.Recalculate(order, TaxRate);
        await _repo.SaveAsync(order, ct);

        return Result.Ok();
    }

    public async Task<Result> DecrementLineAsync(long orderId, long orderLineId, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Fail("Order not found.");

        if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Closed || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refunded)
            return Result.Fail($"Order is {order.Status}. Cannot edit.");

        var line = order.Lines.FirstOrDefault(l => l.Id == orderLineId);
        if (line is null) return Result.Fail("Line not found.");

        // ✅ Sent-to-kitchen lines cannot change qty
        if (line.IsSentToKitchen)
            return Result.Fail("This item was sent to kitchen. Qty change is not allowed.");

        if (line.IsVoided)
            return Result.Fail("This item is removed (voided).");

        if (line.Qty > 1)
        {
            line.Qty -= 1;
        }
        else
        {
            if (!line.IsSentToKitchen)
            {
                order.Lines.Remove(line);
            }
            else
            {
                line.IsVoided = true;
                line.VoidedAt = DateTime.UtcNow;
                line.VoidReason ??= "Removed";
            }
        }

        _calc.Recalculate(order, TaxRate);
        await _repo.SaveAsync(order, ct);

        return Result.Ok();
    }

    public async Task<Result> RemoveLineAsync(long orderId, long orderLineId, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Fail("Order not found.");

        if (order.Status == OrderStatus.Paid || order.Status == OrderStatus.Closed || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Refunded)
            return Result.Fail($"Order is {order.Status}. Cannot edit.");

        var line = order.Lines.FirstOrDefault(l => l.Id == orderLineId);
        if (line is null) return Result.Fail("Line not found.");

        // ✅ Always allow remove => mark voided, keep line visible
        if (line.IsVoided)
            return Result.Fail("This item is already removed.");

        if (!line.IsSentToKitchen)
        {
            order.Lines.Remove(line);
        }
        else
        {
            line.IsVoided = true;
            line.VoidedAt = DateTime.UtcNow;
            line.VoidReason ??= "Removed";
        }

        _calc.Recalculate(order, TaxRate);
        await _repo.SaveAsync(order, ct);

        return Result.Ok();
    }

    // ✅ Kitchen flow
    public async Task<Result> SendToKitchenAsync(long orderId, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Fail("Order not found.");

        // only non-void items count as "has items"
        if (order.Lines.Count(l => !l.IsVoided) == 0)
            return Result.Fail("No items in order.");

        // ✅ kitchen sync timestamps
        var now = DateTime.UtcNow;
        order.KitchenPrevSentAt = order.KitchenLastSentAt;
        order.KitchenLastSentAt = now;

        // ✅ mark current active lines as sent to kitchen (ADD batch)
        foreach (var l in order.Lines.Where(x => !x.IsVoided && !x.IsSentToKitchen))
        {
            l.IsSentToKitchen = true;
            l.SentToKitchenAt = now;
        }

        order.Status = OrderStatus.SentToKitchen;

        _calc.Recalculate(order, TaxRate);
        await _repo.SaveAsync(order, ct);

        // ✅ print delta ticket (ADD + VOID)
        await _printer.PrintKitchenAsync(order, ct);

        // ✅ mark VOID lines as already notified (so VOID prints only once)
        var last = order.KitchenLastSentAt;
        var prev = order.KitchenPrevSentAt;

        if (last.HasValue)
        {
            var voidToMark = order.Lines.Where(l =>
                l.IsVoided
                && l.IsSentToKitchen
                && !l.VoidSentToKitchen
                && l.VoidedAt.HasValue
                && (prev is null
                    ? l.VoidedAt.Value <= last.Value
                    : (l.VoidedAt.Value > prev.Value && l.VoidedAt.Value <= last.Value)))
                .ToList();

            if (voidToMark.Count > 0)
            {
                foreach (var l in voidToMark)
                {
                    l.VoidSentToKitchen = true;
                    l.VoidSentToKitchenAt = now;
                }

                await _repo.SaveAsync(order, ct);
            }
        }

        return Result.Ok();
    }

    public async Task<Result> OpenOrderAsync(long orderId, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Fail("Order not found.");

        if (order.Status != OrderStatus.SentToKitchen)
            return Result.Fail($"Order is {order.Status}. Only 'SentToKitchen' orders can be opened.");

        order.Status = OrderStatus.Editing;
        await _repo.SaveAsync(order, ct);

        return Result.Ok();
    }

    // ✅ Receipt printing (fake now)
    public async Task<Result> PrintReceiptAsync(long orderId, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Fail("Order not found.");

        await _printer.PrintReceiptAsync(order, ct);
        return Result.Ok();
    }
}