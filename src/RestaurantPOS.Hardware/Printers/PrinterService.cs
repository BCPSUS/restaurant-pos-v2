using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;
using RestaurantPOS.Infrastructure.Database;
using System.Text;

namespace RestaurantPOS.Hardware.Printers;

public sealed class PrinterService : IPrinterService
{
    private static readonly string LogsDir = Path.Combine(AppContext.BaseDirectory, "logs");
    private static readonly string KitchenPath = Path.Combine(LogsDir, "print_kitchen.txt");
    private static readonly string ReceiptPath = Path.Combine(LogsDir, "print_receipt.txt");

    private readonly IDbContextFactory<PosDbContext> _dbFactory;

    public PrinterService(IDbContextFactory<PosDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    private async Task<string?> GetTableNoAsync(long? tableId, CancellationToken ct)
    {
        if (!tableId.HasValue) return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var t = await db.Tables.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tableId.Value, ct);

        return t?.TableNo;
    }

    private async Task<string> GetStationAsync(OrderLine line, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(line.StationSnapshot))
            return line.StationSnapshot.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var item = await db.MenuItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == line.MenuItemId, ct);

        if (item is null || string.IsNullOrWhiteSpace(item.Station))
            return "Kitchen";

        return item.Station.Trim();
    }

    private async Task<string> GetKitchenTargetPathAsync(string station, CancellationToken ct)
    {
        var normalized = station?.Trim() ?? "Kitchen";

        StationType stationType;
        if (!Enum.TryParse<StationType>(normalized, true, out stationType))
        {
            stationType = StationType.Kitchen;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var printer = await db.Printers.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.IsEnabled &&
                x.Station == stationType,
                ct);

        if (printer is null || string.IsNullOrWhiteSpace(printer.Target))
            return KitchenPath;

        return Path.Combine(AppContext.BaseDirectory, printer.Target);
    }

    private const int ReceiptWidth = 32; // typical 58mm printer chars

    private static string Clip(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim();
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    private static string LineItem(int qty, string name, decimal total, bool isVoid)
    {
        // Layout: "QTY  NAME..................  12.34"
        if (isVoid) name = $"VOID {name}";
        // qty: 3 chars, total: 7 chars incl decimals
        var qtyPart = qty.ToString().PadLeft(3);
        var totalPart = isVoid ? "".PadLeft(7) : total.ToString("0.00").PadLeft(7);

        var nameMax = ReceiptWidth - (3 + 1 + 1 + 7); // qty + space + space + total        
        var namePart = Clip(name, nameMax).PadRight(nameMax);

        var prefix = isVoid ? "X" : " "; // X = void marker
        return $"{prefix}{qtyPart} {namePart} {totalPart}";
    }

    public async Task PrintKitchenAsync(Order order, CancellationToken ct)
    {
        Directory.CreateDirectory(LogsDir);

        var last = order.KitchenLastSentAt;
        var prev = order.KitchenPrevSentAt;

        // ✅ cache table no once
        var tableNoCached = await GetTableNoAsync(order.TableId, ct);

        // ✅ ADD = items sent in this batch (SentToKitchenAt == KitchenLastSentAt)
        var addLines = order.Lines
            .Where(l => !l.IsVoided
                        && l.IsSentToKitchen
                        && l.SentToKitchenAt.HasValue
                        && last.HasValue
                        && l.SentToKitchenAt.Value == last.Value)
            .OrderBy(l => l.Id)
            .ToList();

        // ✅ VOID = items voided since previous send (or ever if prev is null),
        // but only if they were previously sent to kitchen AND not yet void-notified
        var voidLines = order.Lines
            .Where(l => l.IsVoided
                        && l.IsSentToKitchen
                        && !l.VoidSentToKitchen
                        && l.VoidedAt.HasValue
                        && last.HasValue
                        && (prev is null
                            ? l.VoidedAt.Value <= last.Value
                            : (l.VoidedAt.Value > prev.Value && l.VoidedAt.Value <= last.Value)))
            .OrderBy(l => l.Id)
            .ToList();

        var addGroups = new Dictionary<string, List<OrderLine>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in addLines)
        {
            var station = await GetStationAsync(line, ct);
            if (!addGroups.ContainsKey(station))
                addGroups[station] = new List<OrderLine>();

            addGroups[station].Add(line);
        }

        var voidGroups = new Dictionary<string, List<OrderLine>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in voidLines)
        {
            var station = await GetStationAsync(line, ct);
            if (!voidGroups.ContainsKey(station))
                voidGroups[station] = new List<OrderLine>();

            voidGroups[station].Add(line);
        }

        // nothing to print
        if (addLines.Count == 0 && voidLines.Count == 0)
        {
            var sbNone = new StringBuilder();
            sbNone.AppendLine("=== KITCHEN SYNC TICKET ===");
            sbNone.AppendLine($"Order: {order.OrderNumber}  (Id:{order.Id})");
            sbNone.AppendLine($"Type : {order.OrderType}");
            if (!string.IsNullOrWhiteSpace(tableNoCached))
                sbNone.AppendLine($"Table: {tableNoCached}");
            sbNone.AppendLine($"Time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (last.HasValue)
                sbNone.AppendLine($"Batch: {last.Value:yyyy-MM-dd HH:mm:ss} UTC");
            sbNone.AppendLine(new string('-', 32));
            sbNone.AppendLine("(nothing to sync)");
            sbNone.AppendLine(new string('-', 32));
            sbNone.AppendLine();

            File.AppendAllText(KitchenPath, sbNone.ToString());
            return;
        }

        // ✅ Write ADD ticket (separate)
        foreach (var kv in addGroups)
        {
            var station = kv.Key;
            var lines = kv.Value;

            if (lines.Count == 0)
                continue;

            var addSb = new StringBuilder();
            addSb.AppendLine($"=== {station.ToUpperInvariant()} ADD TICKET ===");           
            addSb.AppendLine($"Order: {order.OrderNumber}  (Id:{order.Id})");
            addSb.AppendLine($"Type : {order.OrderType}");
            if (!string.IsNullOrWhiteSpace(tableNoCached))
                addSb.AppendLine($"Table: {tableNoCached}");
            addSb.AppendLine($"Time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (last.HasValue)
                addSb.AppendLine($"Batch: {last.Value:yyyy-MM-dd HH:mm:ss} UTC");
            addSb.AppendLine(new string('-', 32));

            foreach (var l in lines)
                addSb.AppendLine($"+ {l.Qty,2}  {Clip(l.NameSnapshot, 26)}");

            addSb.AppendLine(new string('-', 32));
            addSb.AppendLine();

            var targetPath = await GetKitchenTargetPathAsync(station, ct);
            File.AppendAllText(targetPath, addSb.ToString());
        }

        // ✅ Write VOID ticket (separate)
        foreach (var kv in voidGroups)
        {
            var station = kv.Key;
            var lines = kv.Value;

            if (lines.Count == 0)
                continue;

            var voidSb = new StringBuilder();
            voidSb.AppendLine($"=== {station.ToUpperInvariant()} VOID TICKET ===");
            voidSb.AppendLine($"Order: {order.OrderNumber}  (Id:{order.Id})");
            voidSb.AppendLine($"Type : {order.OrderType}");
            if (!string.IsNullOrWhiteSpace(tableNoCached))
                voidSb.AppendLine($"Table: {tableNoCached}");
            voidSb.AppendLine($"Time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (last.HasValue)
                voidSb.AppendLine($"Batch: {last.Value:yyyy-MM-dd HH:mm:ss} UTC");
            voidSb.AppendLine(new string('-', 32));

            foreach (var l in lines)
                voidSb.AppendLine($"- {l.Qty,2}  {Clip(l.NameSnapshot, 26)}");

            voidSb.AppendLine(new string('-', 32));
            voidSb.AppendLine();

            var targetPath = await GetKitchenTargetPathAsync(station, ct);
            File.AppendAllText(targetPath, voidSb.ToString());
        }
    }

    public async Task PrintReceiptAsync(Order order, CancellationToken ct)
    {
        Directory.CreateDirectory(LogsDir);

        var sb = new StringBuilder();
        sb.AppendLine("=== RECEIPT ===");
        sb.AppendLine($"Order: {order.OrderNumber}  (Id:{order.Id})");
        sb.AppendLine($"Type : {order.OrderType}");

        var tableNo = !string.IsNullOrWhiteSpace(order.TableNoSnapshot)
    ? order.TableNoSnapshot
    : await GetTableNoAsync(order.TableId, ct);

        if (!string.IsNullOrWhiteSpace(tableNo))
            sb.AppendLine($"Table: {tableNo}");
        sb.AppendLine($"Time : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', 28));

        foreach (var l in order.Lines.OrderBy(x => x.Id))
        {
            sb.AppendLine(LineItem(l.Qty, l.NameSnapshot, l.Total, l.IsVoided));

            if (!string.IsNullOrWhiteSpace(l.Name2Snapshot))
            {
                sb.AppendLine($"    {l.Name2Snapshot}");
            }
        }

        sb.AppendLine(new string('-', ReceiptWidth));
        sb.AppendLine($"{"Subtotal".PadRight(ReceiptWidth - 7)}{order.Subtotal:0.00}".PadLeft(ReceiptWidth));
        sb.AppendLine($"{"Tax".PadRight(ReceiptWidth - 7)}{order.Tax:0.00}".PadLeft(ReceiptWidth));
        sb.AppendLine($"{"Total".PadRight(ReceiptWidth - 7)}{order.Total:0.00}".PadLeft(ReceiptWidth));
        sb.AppendLine();

        File.AppendAllText(ReceiptPath, sb.ToString());
        
    }
}