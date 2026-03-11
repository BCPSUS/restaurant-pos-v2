using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestaurantPOS.Application.Interfaces;
using RestaurantPOS.Application.Services;
using RestaurantPOS.Hardware.Printers;
using RestaurantPOS.Infrastructure.Database;
using RestaurantPOS.Infrastructure.Repositories;
using RestaurantPOS.Domain.Enums;

namespace RestaurantPOS.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public IServiceProvider Services => _host!.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                // ✅ Stable DB path (avoid multiple pos.db copies in bin/debug/project folder)
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RestaurantPOS",
                    "pos.db"
                );
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                var cs = $"Data Source={dbPath}";
                services.AddDbContextFactory<PosDbContext>(opt =>
    opt.UseSqlite(cs, x => x.CommandTimeout(5)));

                services.AddSingleton<OrderCalculator>();
                services.AddScoped<IOrderRepository, OrderRepository>();
                services.AddScoped<IPrinterService, PrinterService>();
                services.AddScoped<IPrinterRepository, PrinterRepository>();
                services.AddScoped<OrderService>();

                // Menu
                services.AddScoped<IMenuRepository, MenuRepository>();
                services.AddScoped<MenuService>();

                services.AddTransient<MainWindow>();
                services.AddTransient<BackOfficeWindow>();
                services.AddTransient<MenuManagementControl>();
            })
            .Build();

        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PosDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();

            await db.Database.MigrateAsync();
            await db.SeedAsync(CancellationToken.None);
            await RestaurantPOS.Infrastructure.Database.DbSeeder.SeedAsync(db, CancellationToken.None);
        }

        var main = _host.Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}