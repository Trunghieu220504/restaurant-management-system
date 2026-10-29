using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Modules.Seating.Infrastructure;

public static class SeatingModule
{
    public static IServiceCollection AddSeatingModule(this IServiceCollection services)
    {
        services.AddDbContext<SeatingDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            SeatingPersistence.Configure(options, connectionString);
        });

        return services;
    }
}
