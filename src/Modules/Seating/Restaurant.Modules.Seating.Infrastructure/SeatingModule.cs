using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Contracts;
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

        services.TryAddSingleton(TimeProvider.System);
        // Sales has no orders yet. A module registered earlier (HIE-3.1) can supply the real guard instead.
        services.TryAddScoped<ISessionClosureGuard, NoOrdersYetClosureGuard>();
        services.AddScoped<ISeatingCatalog, SeatingCatalog>();
        services.AddScoped<ISeatingSessions, SeatingSessions>();
        services.AddScoped<ISessionLookup, SeatingSessionLookup>();

        return services;
    }
}
