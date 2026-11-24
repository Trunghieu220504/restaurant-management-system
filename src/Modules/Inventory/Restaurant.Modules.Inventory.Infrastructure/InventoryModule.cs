using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;

namespace Restaurant.Modules.Inventory.Infrastructure;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddDbContext<InventoryDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            InventoryPersistence.Configure(options, connectionString);
        });

        services.AddScoped<InventoryService>();
        services.AddScoped<Restaurant.Modules.Inventory.Application.IInventory>(p => p.GetRequiredService<InventoryService>());
        services.AddScoped<Restaurant.Modules.Inventory.Application.IInventoryCorrections, InventoryCorrections>();
        services.AddScoped<Restaurant.Modules.Inventory.Application.IStockCounts, StockCounts>();
        return services;
    }
}
