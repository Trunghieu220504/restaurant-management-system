using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Reporting.Infrastructure.Persistence;

namespace Restaurant.Modules.Reporting.Infrastructure;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddDbContext<ReportingDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            ReportingPersistence.Configure(options, connectionString);
        });

        return services;
    }
}
