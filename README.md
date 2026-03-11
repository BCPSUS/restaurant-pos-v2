# RestaurantPOS v2 Template (.NET 8 + WPF + Clean Architecture)

## Prerequisites
- .NET 8 SDK
- Visual Studio 2022 (17.8+) with Desktop development with .NET workload

## How to run
1. Open `RestaurantPOS.sln`
2. Restore NuGet packages
3. Set `RestaurantPOS.App` as startup project
4. Run (F5)

## Architecture
- `RestaurantPOS.App`: WPF UI (Views + ViewModels)
- `RestaurantPOS.Application`: Use cases / services + interfaces
- `RestaurantPOS.Domain`: Entities + enums + value objects
- `RestaurantPOS.Infrastructure`: EF Core (SQLite) + repositories
- `RestaurantPOS.Hardware`: Printers / cash drawer / scanners (stubbed)
- `RestaurantPOS.Shared`: Utilities (Result, Guard, etc.)

## Next steps
- Implement your schema in `PosDbContext`
- Build Order Engine in `RestaurantPOS.Application`
- Implement ESC/POS in `RestaurantPOS.Hardware`
