using Restaurant.Modules.Sales.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Sales.Infrastructure.Messaging;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Sales.Infrastructure;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddDbContext<SalesDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            SalesPersistence.Configure(options, connectionString);
        });

        services.AddScoped<IOrderSettlement, SalesOrderSettlement>();
        services.AddScoped<IOrderStore, EfOrderStore>();
        services.AddScoped<OrderService>();
        services.AddScoped<IOrderCommands>(provider => provider.GetRequiredService<OrderService>());

        // Stages Sales events in the same SaveChanges as the business change. When a second module also needs an
        // IOutboxWriter, give each its own interface: a plain IOutboxWriter registration can only resolve to one writer.
        services.AddScoped<IOutboxWriter, SalesOutboxWriter>();
        services.AddScoped<Restaurant.Modules.Sales.Application.IStaffPortions, StaffPortions>();

        return services;
    }
}
