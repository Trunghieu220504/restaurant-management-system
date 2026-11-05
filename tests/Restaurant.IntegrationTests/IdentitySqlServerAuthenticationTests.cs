using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class IdentitySqlServerAuthenticationTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantIdentityTest_" + Guid.NewGuid().ToString("N");
    private AuthFactory? factory;
    private string originalPassword = string.Empty;
    private string replacementPassword = string.Empty;
    private Guid userId;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        factory = new AuthFactory(connection);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
        originalPassword = RandomPassword();
        replacementPassword = RandomPassword();
        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = "operator", Email = "operator@example.invalid" };
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        Assert.True((await manager.CreateAsync(user, originalPassword)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
        await db.Entry(user).ReloadAsync();
        Assert.True(db.Entry(user).Property<bool>("IsActive").CurrentValue, "A newly created account must be active by default.");
        Assert.True(await manager.CheckPasswordAsync(user, originalPassword), "The generated test password must match its stored hash.");
        userId = user.Id;
    }

    public async Task DisposeAsync()
    {
        if (factory is null) return;
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
            if (target != databaseName || !target.StartsWith("RestaurantIdentityTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe authentication test cleanup target.");
            await db.Database.EnsureDeletedAsync();
        }
        finally { await factory.DisposeAsync(); }
    }

    [SqlServerFact]
    public async Task Login_me_and_logout_revoke_only_current_session_without_leaking_secrets()
    {
        var first = await LoginAsync();
        var second = await LoginAsync();
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        using var client = Client(first.AccessToken);
        var me = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var json = await me.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        var account = JsonSerializer.Deserialize<CurrentAccount>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(userId, account!.Id);
        Assert.Equal(new[] { "Admin" }, account.Roles);
        Assert.All(account.Permissions, code => Assert.StartsWith("identity.", code));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/system/modules")).StatusCode);
        var logout = await client.PostAsync("/api/identity/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("no-store", logout.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
        using var other = Client(second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/identity/me")).StatusCode);
    }

    [SqlServerFact]
    public async Task Password_change_revokes_all_devices_and_requires_new_password()
    {
        var first = await LoginAsync();
        var second = await LoginAsync();
        using var client = Client(first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/identity/change-password",
            new ChangePasswordRequest(originalPassword, replacementPassword))).StatusCode);
        foreach (var token in new[] { first.AccessToken, second.AccessToken })
        {
            using var old = Client(token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/identity/me")).StatusCode);
        }
        using var anonymous = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/identity/login",
            new LoginRequest("operator", originalPassword))).StatusCode);
        await LoginAsync(replacementPassword);
    }

    [SqlServerFact]
    public async Task Wrong_current_password_or_weak_new_password_keeps_existing_sessions()
    {
        var login = await LoginAsync();
        using var client = Client(login.AccessToken);
        foreach (var input in new[] { new ChangePasswordRequest("incorrect", replacementPassword), new ChangePasswordRequest(originalPassword, "x") })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/identity/change-password", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/identity/me")).StatusCode);
        await LoginAsync();
    }

    [SqlServerFact]
    public async Task Five_wrong_passwords_lock_account_and_unknown_user_has_same_error()
    {
        using var client = factory!.CreateClient();
        string? failure = null;
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("operator", "wrong"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            failure = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();
        }
        var locked = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("operator", originalPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        var unknown = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("missing", "wrong"));
        Assert.Equal(failure, (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(userId.ToString()))!));
    }

    [SqlServerFact]
    public async Task Rate_limit_rejects_excess_login_attempts()
    {
        using var client = factory!.CreateClient();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("", ""))).StatusCode);
    }

    [SqlServerFact]
    public async Task Disabled_account_cannot_login_or_use_an_existing_token()
    {
        var login = await LoginAsync();
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            db.Entry(user).Property<bool>("IsActive").CurrentValue = false;
            await db.SaveChangesAsync();
        }
        using var client = Client(login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("operator", originalPassword))).StatusCode);
    }

    [SqlServerFact]
    public async Task Current_roles_replace_stale_token_roles()
    {
        var login = await LoginAsync();
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "Admin")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Staff")).Succeeded);
        }
        using var client = Client(login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/system/modules")).StatusCode);
        var account = await client.GetFromJsonAsync<CurrentAccount>("/api/identity/me");
        Assert.Equal(new[] { "Staff" }, account!.Roles);
        Assert.DoesNotContain("identity.roles.manage", account.Permissions);
    }

    [SqlServerFact]
    public async Task Expired_session_and_changed_security_stamp_are_rejected()
    {
        var login = await LoginAsync();
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Set<LoginSession>().Where(s => s.UserId == userId).ExecuteUpdateAsync(set =>
                set.SetProperty(s => s.CreatedAt, DateTime.UtcNow.AddHours(-1)).SetProperty(s => s.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }
        using var expired = Client(login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync("/api/identity/me")).StatusCode);
        var active = await LoginAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            Assert.True((await manager.UpdateSecurityStampAsync((await manager.FindByIdAsync(userId.ToString()))!)).Succeeded);
        }
        using var changed = Client(active.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await changed.GetAsync("/api/identity/me")).StatusCode);
    }

    [SqlServerFact]
    public async Task Concurrent_password_changes_cannot_both_succeed()
    {
        var first = await LoginAsync();
        var second = await LoginAsync();
        using var one = Client(first.AccessToken);
        using var two = Client(second.AccessToken);
        var results = await Task.WhenAll(
            one.PostAsJsonAsync("/api/identity/change-password", new ChangePasswordRequest(originalPassword, replacementPassword)),
            two.PostAsJsonAsync("/api/identity/change-password", new ChangePasswordRequest(originalPassword, RandomPassword())));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task Password_write_failure_rolls_back_password_and_session_revocation()
    {
        var login = await LoginAsync();
        var failure = new FailSessionRevocation();
        using (var failing = new AuthFactory(factory!.Connection, failure))
        using (var client = failing.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            var response = await client.PostAsJsonAsync("/api/identity/change-password", new ChangePasswordRequest(originalPassword, replacementPassword));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        Assert.True(failure.Triggered, "The fault must happen after the password write, not at an earlier unrelated failure.");
        using var original = Client(login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await original.GetAsync("/api/identity/me")).StatusCode);
        await LoginAsync();
    }

    private async Task<LoginResult> LoginAsync(string? password = null)
    {
        using var client = factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("operator", password ?? originalPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<LoginResult>())!;
    }

    [SqlServerFact]
    public async Task Valid_signature_cannot_bypass_expiry_session_ownership_or_session_existence()
    {
        var login = await LoginAsync();
        var sessionId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims.Single(c => c.Type == "sid").Value);
        await using var scope = factory!.Services.CreateAsyncScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        var now = DateTime.UtcNow;
        foreach (var token in new[]
        {
            issuer.Issue(userId, sessionId, ["Admin"], now.AddHours(-2), now.AddHours(-1)),
            issuer.Issue(Guid.NewGuid(), sessionId, ["Admin"], now, now.AddMinutes(5)),
            issuer.Issue(userId, Guid.NewGuid(), ["Admin"], now, now.AddMinutes(5))
        })
        {
            using var client = Client(token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
        }
    }

    private HttpClient Client(string token)
    {
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RandomPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";

    private sealed class AuthFactory(string connection, IInterceptor? interceptor = null) : WebApplicationFactory<Program>
    {
        public string Connection => connection;
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
            if (interceptor is not null)
                builder.ConfigureServices(services => services.AddDbContext<IdentityDbContext>(options => options.AddInterceptors(interceptor)));
        }
    }

    private sealed class FailSessionRevocation : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("[identity].[LoginSession]", StringComparison.Ordinal))
            {
                Triggered = true;
                throw new InvalidOperationException("Injected failure after password write and before session revocation.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
