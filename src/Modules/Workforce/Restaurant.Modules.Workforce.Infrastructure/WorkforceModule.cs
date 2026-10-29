using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Workforce.Infrastructure.Persistence;

namespace Restaurant.Modules.Workforce.Infrastructure;

public static class WorkforceModule
{
    public static IServiceCollection AddWorkforceModule(this IServiceCollection services)
    {
        services.AddDbContext<WorkforceDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            WorkforcePersistence.Configure(options, connectionString);
        });

        return services;
    }
}
