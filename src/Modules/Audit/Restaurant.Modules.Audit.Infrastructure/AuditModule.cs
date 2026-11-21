using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Restaurant.Modules.Audit.Application;
using Restaurant.Modules.Audit.Contracts;
using Restaurant.Modules.Audit.Infrastructure.Persistence;

namespace Restaurant.Modules.Audit.Infrastructure;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddDbContext<AuditDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            AuditPersistence.Configure(options, connectionString);
        });

        return services;
    }
}
