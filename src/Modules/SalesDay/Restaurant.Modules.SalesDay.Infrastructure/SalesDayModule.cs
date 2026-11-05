using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;

namespace Restaurant.Modules.SalesDay.Infrastructure;

public static class SalesDayModule
{
    public static IServiceCollection AddSalesDayModule(this IServiceCollection services)
    {
        services.AddDbContext<SalesDayDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            SalesDayPersistence.Configure(options, connectionString);
        });

        return services;
    }
}
