using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Restaurant.Modules.Fulfillment.Application;

namespace Restaurant.Modules.Fulfillment.Infrastructure;

public static class FulfillmentModule
{
    public static IServiceCollection AddFulfillmentModule(this IServiceCollection services)
    {
        services.AddDbContext<FulfillmentDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            FulfillmentPersistence.Configure(options, connectionString);
        });

        services.AddScoped<IIntegrationEventHandler, Messaging.BatchConfirmedHandler>();
        services.AddScoped<IFulfillmentService, FulfillmentService>();
        services.AddScoped<IFulfillmentIncidents, FulfillmentIncidents>();

        return services;
    }
}
