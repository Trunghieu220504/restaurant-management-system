using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Infrastructure.Persistence;

namespace Restaurant.Modules.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddDbContext<IdentityDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("Restaurant");
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            IdentityPersistence.Configure(options, connectionString);
        });

        services.AddIdentityCore<IdentityUser<Guid>>(options =>
        {
            options.Password.RequiredLength = 12;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        return services;
    }
}
