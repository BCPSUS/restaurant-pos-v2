using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Domain.Entities;
using RestaurantPOS.Domain.Enums;

namespace RestaurantPOS.Infrastructure.Database;

public static class DbSeeder
{

    public static async Task SeedAsync(PosDbContext db, CancellationToken ct = default)
    {
        // Ensure DB exists / migrated
        await db.Database.MigrateAsync(ct);

        // -----------------------------
        // Seed Menu Categories
        // -----------------------------
        if (!await db.MenuCategories.AnyAsync(ct))
        {
            var sushi = new MenuCategory { Name = "Sushi", Name2 = "寿司", SortOrder = 1, IsActive = true };
            var kitchen = new MenuCategory { Name = "Kitchen", Name2 = "热菜", SortOrder = 2, IsActive = true };
            var drink = new MenuCategory { Name = "Drink", Name2 = "饮料", SortOrder = 3, IsActive = true };

            db.MenuCategories.AddRange(sushi, kitchen, drink);
            await db.SaveChangesAsync(ct);

            // -----------------------------
            // Seed Menu Items
            // -----------------------------
            db.MenuItems.AddRange(
    new MenuItem
    {
        CategoryId = sushi.Id,
        ItemNo = "S001",
        Name = "California Roll",
        Name2 = "加州卷",
        Price = 8.99m,
        SortOrder = 1,
        IsActive = true,
        Station = "Sushi"
    },
    new MenuItem
    {
        CategoryId = sushi.Id,
        ItemNo = "S002",
        Name = "Salmon Nigiri",
        Name2 = "三文鱼握寿司",
        Price = 6.50m,
        SortOrder = 2,
        IsActive = true,
        Station = "Sushi"
    },

    new MenuItem
    {
        CategoryId = kitchen.Id,
        ItemNo = "K001",
        Name = "Chicken Teriyaki",
        Name2 = "照烧鸡",
        Price = 12.99m,
        SortOrder = 1,
        IsActive = true,
        Station = "Kitchen"
    },
    new MenuItem
    {
        CategoryId = kitchen.Id,
        ItemNo = "K002",
        Name = "Tonkotsu Ramen",
        Name2 = "豚骨拉面",
        Price = 13.99m,
        SortOrder = 2,
        IsActive = true,
        Station = "Kitchen"
    },

    new MenuItem
    {
        CategoryId = drink.Id,
        ItemNo = "D001",
        Name = "Coke",
        Name2 = "可乐",
        Price = 2.50m,
        SortOrder = 1,
        IsActive = true,
        Station = "Bar"
    },
    new MenuItem
    {
        CategoryId = drink.Id,
        ItemNo = "D002",
        Name = "Green Tea",
        Name2 = "绿茶",
        Price = 2.00m,
        SortOrder = 2,
        IsActive = true,
        Station = "Bar"
    }

);
            // -----------------------------
            // Backfill Station for existing menu items
            // -----------------------------
            var categories = await db.MenuCategories
                .AsNoTracking()
                .ToListAsync(ct);

            var items = await db.MenuItems.ToListAsync(ct);

            foreach (var item in items)
            {
                // skip if already assigned
                if (!string.IsNullOrWhiteSpace(item.Station))
                    continue;

                var cat = categories.FirstOrDefault(c => c.Id == item.CategoryId);
                var catName = cat?.Name?.Trim() ?? "";

                if (catName.Equals("Sushi", StringComparison.OrdinalIgnoreCase))
                    item.Station = "Sushi";
                else if (catName.Equals("Drink", StringComparison.OrdinalIgnoreCase))
                    item.Station = "Bar";
                else
                    item.Station = "Kitchen";
            }
            

            await db.SaveChangesAsync(ct);

            
        }

        var existingItems = await db.MenuItems.ToListAsync(ct);

        foreach (var item in existingItems)
        {
            if (item.ItemNo.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                item.Station = "Sushi";
            else if (item.ItemNo.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                item.Station = "Bar";
            else
                item.Station = "Kitchen";
        }

        await db.SaveChangesAsync(ct);

        if (!db.Printers.Any())
        {
            db.Printers.AddRange(
                new Printer
                {
                    Name = "Sushi Printer",
                    PrinterType = PrinterType.FakeFile,
                    Station = StationType.Sushi,
                    Target = "logs/print_sushi.txt",
                    IsEnabled = true,
                    SortOrder = 1
                },
                new Printer
                {
                    Name = "Kitchen Printer",
                    PrinterType = PrinterType.FakeFile,
                    Station = StationType.Kitchen,
                    Target = "logs/print_kitchen.txt",
                    IsEnabled = true,
                    SortOrder = 2
                },
                new Printer
                {
                    Name = "Bar Printer",
                    PrinterType = PrinterType.FakeFile,
                    Station = StationType.Bar,
                    Target = "logs/print_bar.txt",
                    IsEnabled = true,
                    SortOrder = 3
                }
            );

            await db.SaveChangesAsync();
        }
    }


}