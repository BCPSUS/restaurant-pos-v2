using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Domain.Entities;

namespace RestaurantPOS.Infrastructure.Database;

public sealed class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    // Orders
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    // Tables
    public DbSet<TableEntity> Tables => Set<TableEntity>();

    // Menu
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    // Printers
    public DbSet<Printer> Printers => Set<Printer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // -----------------------------
        // Tables
        // -----------------------------
        modelBuilder.Entity<TableEntity>(b =>
        {
            b.ToTable("Tables");

            b.HasKey(x => x.Id);

            b.Property(x => x.TableNo)
                .IsRequired()
                .HasMaxLength(20);

            b.HasIndex(x => x.TableNo)
                .IsUnique();

            b.Property(x => x.IsActive)
                .HasDefaultValue(true);
        });

        // -----------------------------
        // Orders
        // -----------------------------
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");

            b.HasKey(x => x.Id);

            b.HasOne<TableEntity>()
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.TableId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(o => o.Lines)
                .WithOne()
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // -----------------------------
        // Menu Categories
        // -----------------------------
        modelBuilder.Entity<MenuCategory>(b =>
        {
            b.ToTable("MenuCategories");

            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(x => x.SortOrder)
                .HasDefaultValue(0);

            b.Property(x => x.IsActive)
                .HasDefaultValue(true);

            b.HasIndex(x => x.Name)
                .IsUnique();

            b.HasMany(c => c.Items)
                .WithOne(i => i.Category)
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -----------------------------
        // Menu Items
        // -----------------------------
        modelBuilder.Entity<MenuItem>(b =>
        {
            b.ToTable("MenuItems");

            b.HasKey(x => x.Id);

            b.Property(x => x.ItemNo)
                .IsRequired()
                .HasMaxLength(50);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(x => x.Name2)
                .HasMaxLength(200);

            b.Property(x => x.Price)
                .HasPrecision(18, 2);

            b.Property(x => x.Station)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Kitchen");

            b.Property(x => x.SortOrder)
                .HasDefaultValue(0);

            b.Property(x => x.IsActive)
                .HasDefaultValue(true);

            // ItemNo 全局唯一
            b.HasIndex(x => x.ItemNo)
                .IsUnique();

            // 分类内排序常用索引
            b.HasIndex(x => new { x.CategoryId, x.SortOrder });
        });

        // -----------------------------
        // Printers
        // -----------------------------
        modelBuilder.Entity<Printer>(b =>
        {
            b.ToTable("Printers");

            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(x => x.Target)
                .IsRequired()
                .HasMaxLength(300);
        });
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        // Seed default tables if empty
        if (!await Tables.AnyAsync(ct))
        {
            for (int i = 1; i <= 20; i++)
            {
                Tables.Add(new TableEntity
                {
                    TableNo = i.ToString(),
                    IsActive = true
                });
            }

            await SaveChangesAsync(ct);
        }
    }
}