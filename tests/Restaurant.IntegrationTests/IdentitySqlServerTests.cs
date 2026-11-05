using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure;
using Restaurant.Modules.Identity.Infrastructure.Initialization;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER")))
        {
            Skip = "Requires an isolated SQL Server test instance via IDENTITY_TEST_SQLSERVER.";
        }
    }
}

public sealed class IdentitySqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantIdentityTest_" + Guid.NewGuid().ToString("N");
    private string connectionString = string.Empty;
    private ServiceProvider? provider;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return;
        }
        var builder = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName };
        connectionString = builder.ConnectionString;
        provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (provider is null)
        {
            return;
        }
        try
        {
            // Never delete the caller's database: only this test's generated database.
            if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName ||
                !databaseName.StartsWith("RestaurantIdentityTest_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsafe test database cleanup target.");
            }
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureDeletedAsync();
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Migration_is_repeatable_and_does_not_reset_modified_grants()
    {
        await using var scope = provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(5, await db.Roles.CountAsync());
        Assert.Equal(26, await db.Set<Permission>().CountAsync());
        var grant = await db.Set<RolePermission>().FirstAsync();
        db.Remove(grant);
        await db.SaveChangesAsync();
        var count = await db.Set<RolePermission>().CountAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(count, await db.Set<RolePermission>().CountAsync());
        Assert.Empty(await db.Users.ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Bootstrap_hashes_password_and_refuses_a_second_run_without_reset()
    {
        var input = Input("first");
        await BootstrapAsync(input);
        await using var scope = provider!.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var user = await users.FindByNameAsync(input.UserName);
        Assert.NotNull(user);
        Assert.NotEqual(input.Password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, input.Password));
        Assert.Equal(new[] { "Admin" }, await users.GetRolesAsync(user));
        await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAsync(Input("second")));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.CountAsync());
    }

    [SqlServerFact]
    public async Task Bootstrap_never_elevates_an_existing_staff_account()
    {
        var input = Input("existing");
        await using var scope = provider!.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = input.UserName, Email = input.Email };
        Assert.True((await users.CreateAsync(user, input.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Staff")).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAsync(input));
        Assert.Equal(new[] { "Staff" }, await users.GetRolesAsync(user));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<AdminBootstrap>().ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_bootstrap_creates_exactly_one_admin()
    {
        async Task<bool> AttemptAsync(string name)
        {
            try { await BootstrapAsync(Input(name)); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(AttemptAsync("concurrent1"), AttemptAsync("concurrent2"));
        Assert.Single(results, r => r);
        await using var scope = provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.UserRoles.CountAsync());
        Assert.Equal(1, await db.Set<AdminBootstrap>().CountAsync());
    }

    [SqlServerFact]
    public async Task Weak_password_cannot_create_an_account()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAsync(
            new AdminBootstrapInput("weak", "weak@example.invalid", "x")));
        await using var scope = provider!.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.ToArrayAsync());
    }

    [SqlServerFact]
    public async Task Failure_after_user_and_role_writes_rolls_back_everything()
    {
        await using var failingProvider = CreateProvider(new FailBootstrapSave());
        await using (var scope = failingProvider.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().InitializeAsync(Input("rollback")));
        }
        await using var verification = provider!.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Empty(await db.Users.ToArrayAsync());
        Assert.Empty(await db.UserRoles.ToArrayAsync());
        Assert.Empty(await db.Set<AdminBootstrap>().ToArrayAsync());
    }

    private ServiceProvider CreateProvider(IInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connectionString }).Build());
        services.AddLogging();
        services.AddIdentityModule();
        services.AddScoped<AdminBootstrapper>();
        if (interceptor is not null)
        {
            services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(interceptor));
        }
        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task BootstrapAsync(AdminBootstrapInput input)
    {
        await using var scope = provider!.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().InitializeAsync(input);
    }

    private static AdminBootstrapInput Input(string userName) => new(userName,
        userName + "@example.invalid", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!");

    private sealed class FailBootstrapSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AdminBootstrap>().Any(e => e.State == EntityState.Added))
            {
                throw new InvalidOperationException("Injected bootstrap persistence failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
