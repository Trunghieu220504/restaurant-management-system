using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Audit.Domain;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Identity.Application.Accounts;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class IdentityBusinessAuditSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantIdentityAuditTest_" + Guid.NewGuid().ToString("N");
    private readonly string password = RandomPassword();
    private readonly AuditFailure failure = new();
    private AuditFactory? factory;
    private Guid adminId;
    private Guid staffId;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        factory = new AuditFactory(connection, failure);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await BusinessAuditTestSupport.MigrateAsync(connection);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        foreach (var role in new[] { "Admin", "Staff" })
        {
            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = role, Email = role + "@example.invalid" };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            if (role == "Admin") adminId = user.Id;
            else staffId = user.Id;
        }
    }

    public async Task DisposeAsync()
    {
        if (factory is null) return;
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
            if (target != databaseName || !target.StartsWith("RestaurantIdentityAuditTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe Identity audit test cleanup target.");
            await db.Database.EnsureDeletedAsync();
        }
        finally { await factory.DisposeAsync(); }
    }

    [SqlServerFact]
    public async Task Account_writes_are_audited_once_and_no_ops_or_rejections_add_nothing()
    {
        var created = await AccountsAsync(a => a.CreateAsync(adminId,
            new CreateAccountRequest("new-account", "new@example.invalid", password, ["Staff"]), default));
        Assert.True(created.Succeeded);
        var id = created.Value!.Id;
        Assert.True((await AccountsAsync(a => a.SetRolesAsync(adminId, id, new SetRolesRequest(["Kitchen"]), default))).Succeeded);
        Assert.True((await AccountsAsync(a => a.SetRolesAsync(adminId, id, new SetRolesRequest(["Kitchen"]), default))).Succeeded);
        Assert.True((await AccountsAsync(a => a.SetActiveAsync(adminId, id, false, default))).Succeeded);
        Assert.True((await AccountsAsync(a => a.SetActiveAsync(adminId, id, false, default))).Succeeded);
        Assert.True((await AccountsAsync(a => a.SetActiveAsync(adminId, id, true, default))).Succeeded);
        Assert.True((await AccountsAsync(a => a.ResetPasswordAsync(adminId, id, new ResetPasswordRequest(RandomPassword()), default))).Succeeded);
        Assert.False((await AccountsAsync(a => a.SetActiveAsync(staffId, id, false, default))).Succeeded);

        var entries = await EntriesAsync();
        Assert.Equal(new[] { "account.activate", "account.create", "account.deactivate", "account.reset-password", "account.roles" },
            entries.Select(e => e.Action).Order(StringComparer.Ordinal));
        Assert.All(entries, e =>
        {
            Assert.Equal(adminId, e.ActorId);
            Assert.Equal("Account", e.EntityType);
            Assert.Equal(id.ToString(), e.EntityId);
            Assert.Null(e.Reason);
        });
    }

    [SqlServerFact]
    public async Task Authentication_events_use_account_or_session_ids_and_replays_do_not_duplicate_revocations()
    {
        Assert.Null(await AuthAsync(a => a.LoginAsync(new LoginRequest("unknown", password), default)));
        Assert.Null(await AuthAsync(a => a.LoginAsync(new LoginRequest("Staff", RandomPassword()), default)));
        var first = await LoginAsync();
        var rotated = await AuthAsync(a => a.RefreshAsync(first.RefreshToken, default));
        Assert.NotNull(rotated);
        Assert.Null(await AuthAsync(a => a.RefreshAsync(first.RefreshToken, default)));
        Assert.Null(await AuthAsync(a => a.RefreshAsync(first.RefreshToken, default)));
        Assert.Equal(AccountMutationResult.Success, await AuthAsync(a => a.LogoutByRefreshTokenAsync(rotated.RefreshToken, default)));

        var second = await LoginAsync();
        Assert.Equal(AccountMutationResult.Success, await AuthAsync(a => a.LogoutByRefreshTokenAsync(second.RefreshToken, default)));
        Assert.Equal(AccountMutationResult.Success, await AuthAsync(a => a.LogoutByRefreshTokenAsync(second.RefreshToken, default)));
        var third = await LoginAsync();
        Assert.Equal(AccountMutationResult.Success, await AuthAsync(a => a.LogoutAsync(staffId, SessionId(third), default)));
        Assert.Equal(AccountMutationResult.Unauthorized, await AuthAsync(a => a.LogoutAsync(staffId, SessionId(third), default)));
        var fourth = await LoginAsync();
        Assert.Equal(AccountMutationResult.Success, await AuthAsync(a => a.ChangePasswordAsync(staffId, SessionId(fourth),
            new ChangePasswordRequest(password, RandomPassword()), default)));

        var entries = await EntriesAsync();
        Assert.Equal(10, entries.Length);
        Assert.Equal(4, entries.Count(e => e.Action == "login.success"));
        Assert.Equal(2, entries.Count(e => e.Action == "logout"));
        Assert.Single(entries, e => e.Action == "refresh.rotate");
        Assert.Single(entries, e => e.Action == "refresh.replay-revoke" && e.Reason == "refresh-token-reuse");
        Assert.Single(entries, e => e.Action == "login.failure" && e.Reason == "invalid-credentials");
        Assert.Single(entries, e => e.Action == "password.change");
        var sessions = new[] { SessionId(first), SessionId(second), SessionId(third), SessionId(fourth) };
        Assert.All(entries, e =>
        {
            Assert.Equal(staffId, e.ActorId);
            Assert.Equal("Identity", e.SourceModule);
            Assert.Null(e.BusinessDayId);
            if (e.EntityType == "Account") Assert.Equal(staffId.ToString(), e.EntityId);
            else
            {
                Assert.Equal("LoginSession", e.EntityType);
                Assert.Contains(Guid.Parse(e.EntityId), sessions);
            }
            Assert.True(e.Reason is null or "invalid-credentials" or "refresh-token-reuse");
        });
    }

    [SqlServerFact]
    public async Task Audit_failure_rolls_back_account_creation_and_role_assignment()
    {
        failure.Armed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => AccountsAsync(a => a.CreateAsync(adminId,
            new CreateAccountRequest("rolled-back", "rollback@example.invalid", password, ["Staff"]), default)));
        Assert.True(failure.Triggered);
        await using var scope = factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(2, await db.UserRoles.CountAsync());
        Assert.Empty(await EntriesAsync());
    }

    [SqlServerFact]
    public async Task Audit_failure_rolls_back_failed_login_counter_and_successful_login_credentials()
    {
        failure.Armed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuthAsync(a => a.LoginAsync(new LoginRequest("Staff", RandomPassword()), default)));
        Assert.True(failure.Triggered);
        failure.Triggered = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => LoginAsync());
        Assert.True(failure.Triggered);
        await using var scope = factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.Id == staffId)).AccessFailedCount);
        Assert.Empty(await db.Set<LoginSession>().ToArrayAsync());
        Assert.Empty(await db.Set<RefreshToken>().ToArrayAsync());
        Assert.Empty(await EntriesAsync());
    }

    [SqlServerFact]
    public async Task Audit_failure_rolls_back_password_change_and_session_revocation()
    {
        var login = await LoginAsync();
        failure.Armed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuthAsync(a => a.ChangePasswordAsync(staffId, SessionId(login),
            new ChangePasswordRequest(password, RandomPassword()), default)));
        Assert.True(failure.Triggered);
        await using var scope = factory!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(staffId.ToString()))!, password));
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Null((await db.Set<LoginSession>().SingleAsync()).RevokedAt);
        Assert.Null((await db.Set<RefreshToken>().SingleAsync()).RevokedAt);
        Assert.Equal("login.success", Assert.Single(await EntriesAsync()).Action);
    }

    private async Task<T> AccountsAsync<T>(Func<IAccountManagement, Task<T>> action)
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAccountManagement>());
    }

    private async Task<T> AuthAsync<T>(Func<IIdentityAuthentication, Task<T>> action)
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IIdentityAuthentication>());
    }

    private async Task<LoginResult> LoginAsync() =>
        Assert.IsType<LoginResult>(await AuthAsync(a => a.LoginAsync(new LoginRequest("Staff", password), default)));

    private async Task<NhatKy[]> EntriesAsync()
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Entries.AsNoTracking().ToArrayAsync();
    }

    private static Guid SessionId(LoginResult login) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims.Single(c => c.Type == "sid").Value);

    private static string RandomPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";

    private sealed class AuditFailure
    {
        public bool Armed { get; set; }
        public bool Triggered { get; set; }
    }

    private sealed class FailAfterRealAudit(IBusinessAudit inner, AuditFailure failure) : IBusinessAudit
    {
        public async Task AppendAsync(BusinessAuditEntry entry, CancellationToken cancellationToken)
        {
            await inner.AppendAsync(entry, cancellationToken);
            if (!failure.Armed) return;
            failure.Triggered = true;
            throw new InvalidOperationException("Injected failure after the real audit insert.");
        }
    }

    private sealed class AuditFactory(string connection, AuditFailure failure) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Restaurant"] = connection,
                ["Jwt:Issuer"] = "restaurant-tests",
                ["Jwt:Audience"] = "restaurant-tests-client",
                ["Jwt:SigningKey"] = ApiFactory.SigningKey
            }));
            builder.ConfigureServices(services => services.AddScoped<IBusinessAudit>(provider => new FailAfterRealAudit(
                new BusinessAudit(provider, provider.GetRequiredService<IHttpContextAccessor>(), provider.GetRequiredService<TimeProvider>()), failure)));
        }
    }
}
