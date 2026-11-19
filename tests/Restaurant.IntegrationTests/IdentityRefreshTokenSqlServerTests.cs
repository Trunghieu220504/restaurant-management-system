using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>HIE-1.3: refresh token rotation, reuse detection and revocation. Needs IDENTITY_TEST_SQLSERVER.</summary>
public sealed class IdentityRefreshTokenSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantIdentityTest_" + Guid.NewGuid().ToString("N");
    private RefreshFactory? factory;
    private string password = string.Empty;
    private Guid userId;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        factory = new RefreshFactory(connection);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";
        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = "operator", Email = "operator@example.invalid" };
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        Assert.True((await manager.CreateAsync(user, password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, "Admin")).Succeeded);
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
                throw new InvalidOperationException("Unsafe refresh test cleanup target.");
            await db.Database.EnsureDeletedAsync();
        }
        finally { await factory.DisposeAsync(); }
    }

    [SqlServerFact]
    public async Task Login_sets_httponly_cookie_and_never_returns_the_secret_in_json()
    {
        var (response, secret) = await LoginRawAsync();
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rt=", StringComparison.Ordinal));
        foreach (var attribute in new[] { "httponly", "secure", "samesite=strict", "path=/api/identity" })
            Assert.Contains(attribute, setCookie, StringComparison.OrdinalIgnoreCase);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        // Only a SHA-256 hash is stored, never the secret itself.
        await using var scope = factory!.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<RefreshToken>().AsNoTracking().SingleAsync();
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(secret)), stored.TokenHash);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [SqlServerFact]
    public async Task Refresh_rotates_the_secret_keeps_the_session_and_keeps_the_absolute_expiry()
    {
        var (login, first) = await LoginAsync();
        var (refreshed, second) = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(first, second.Secret);
        var result = second.Result;
        Assert.NotEqual(login.AccessToken, result.AccessToken);
        Assert.Equal(userId, result.Account.Id);
        Assert.Equal(SessionId(login.AccessToken), SessionId(result.AccessToken));
        using (var client = Bearer(result.AccessToken))
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/identity/me")).StatusCode);

        await using var scope = factory!.Services.CreateAsyncScope();
        var tokens = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<RefreshToken>().AsNoTracking()
            .OrderBy(t => t.CreatedAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].UsedAt);
        Assert.Equal(tokens[1].Id, tokens[0].ReplacedByTokenId);
        Assert.Equal(tokens[0].ExpiresAt, tokens[1].ExpiresAt);
    }

    [SqlServerFact]
    public async Task Refresh_works_after_the_access_token_has_expired()
    {
        var (login, secret) = await LoginAsync();
        await ExecuteAsync("UPDATE [identity].[LoginSession] SET ExpiresAt = DATEADD(minute, -5, SYSUTCDATETIME()), CreatedAt = DATEADD(minute, -20, SYSUTCDATETIME())");
        using (var client = Bearer(login.AccessToken))
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
        var (response, next) = await RefreshAsync(secret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var renewed = Bearer(next.Result.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await renewed.GetAsync("/api/identity/me")).StatusCode);
    }

    [SqlServerFact]
    public async Task Reusing_a_rotated_secret_after_the_grace_window_revokes_the_whole_session()
    {
        var (_, first) = await LoginAsync();
        var (_, second) = await RefreshAsync(first);
        await ExecuteAsync("UPDATE [identity].[RefreshToken] SET UsedAt = DATEADD(minute, -5, SYSUTCDATETIME()) WHERE UsedAt IS NOT NULL");

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(first)).StatusCode);

        using var client = Bearer(second.Result.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(second.Secret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Reusing_a_just_rotated_secret_revokes_the_session_immediately()
    {
        var (_, first) = await LoginAsync();
        var (_, second) = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(first)).StatusCode);

        using var client = Bearer(second.Result.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(second.Secret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Concurrent_refreshes_with_the_same_secret_issue_exactly_one_new_token()
    {
        var (_, secret) = await LoginAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => RefreshRawAsync(secret))));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
        await using var scope = factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(2, await db.Set<RefreshToken>().CountAsync());
        Assert.NotNull((await db.Set<LoginSession>().AsNoTracking().SingleAsync()).RevokedAt);
    }

    [SqlServerFact]
    public async Task Expired_missing_or_garbage_secrets_are_rejected()
    {
        var (_, secret) = await LoginAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync("not-a-real-secret")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(new string('x', 500))).StatusCode);
        await ExecuteAsync("UPDATE [identity].[RefreshToken] SET CreatedAt = DATEADD(day, -9, SYSUTCDATETIME()), ExpiresAt = DATEADD(day, -1, SYSUTCDATETIME())");
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(secret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Logout_revokes_the_refresh_secret_and_clears_the_cookie()
    {
        var (login, secret) = await LoginAsync();
        using var client = Bearer(login.AccessToken);
        var logout = await client.PostAsync("/api/identity/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = logout.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rt=", StringComparison.Ordinal));
        Assert.Contains("expires=", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(secret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Logout_of_one_device_does_not_revoke_the_refresh_secret_of_another()
    {
        var (first, firstSecret) = await LoginAsync();
        var (_, secondSecret) = await LoginAsync();
        using (var client = Bearer(first.AccessToken)) await client.PostAsync("/api/identity/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(firstSecret)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshRawAsync(secondSecret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Password_change_revokes_every_refresh_secret_of_the_account()
    {
        var (first, firstSecret) = await LoginAsync();
        var (_, secondSecret) = await LoginAsync();
        using var client = Bearer(first.AccessToken);
        var change = await client.PostAsJsonAsync("/api/identity/change-password",
            new ChangePasswordRequest(password, password + "Zz9#"));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Contains(change.Headers.GetValues("Set-Cookie"), c => c.StartsWith("rt=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(firstSecret)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(secondSecret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Refresh_is_rejected_for_a_disabled_account()
    {
        var (_, secret) = await LoginAsync();
        await ExecuteAsync($"UPDATE [identity].[AspNetUsers] SET IsActive = 0 WHERE Id = '{userId}'");
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(secret)).StatusCode);
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    [SqlServerFact]
    public async Task Token_issuer_failure_rolls_back_rotation_and_preserves_the_original_secret()
    {
        var (_, secret) = await LoginAsync();
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var service = new Restaurant.Modules.Identity.Infrastructure.Authentication.IdentityAuthentication(
                db, scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>(),
                new FailingIssuer(), TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync(secret, CancellationToken.None));
        }
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var token = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                .Set<RefreshToken>().AsNoTracking().SingleAsync();
            Assert.Null(token.UsedAt);
            Assert.Null(token.ReplacedByTokenId);
        }
        Assert.Equal(HttpStatusCode.OK, (await RefreshRawAsync(secret)).StatusCode);
    }

    [SqlServerFact]
    public async Task Refresh_returns_current_roles_and_permissions_after_role_changes()
    {
        var (_, secret) = await LoginAsync();
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var user = (await manager.FindByIdAsync(userId.ToString()))!;
            Assert.True((await manager.RemoveFromRoleAsync(user, "Admin")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, "Manager")).Succeeded);
        }
        var (response, next) = await RefreshAsync(secret);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Manager", next.Result.Account.Roles);
        Assert.DoesNotContain("Admin", next.Result.Account.Roles);
        Assert.Contains("salesday.manage", next.Result.Account.Permissions);
    }

    private sealed class FailingIssuer : IAccessTokenIssuer
    {
        public string Issue(Guid userId, Guid sessionId, string[] roles, DateTime issuedAt, DateTime expiresAt) =>
            throw new InvalidOperationException("Injected issuer failure.");
    }

    [SqlServerFact]
    public async Task Logout_with_only_refresh_cookie_revokes_an_expired_access_session()
    {
        var (_, secret) = await LoginAsync();
        await ExecuteAsync("UPDATE [identity].[LoginSession] SET ExpiresAt = DATEADD(minute, -5, SYSUTCDATETIME()), CreatedAt = DATEADD(minute, -20, SYSUTCDATETIME())");
        using var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/identity/logout");
        request.Headers.Add("Cookie", "rt=" + secret);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("rt=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(secret)).StatusCode);
    }

    private async Task<(HttpResponseMessage Response, string Secret)> LoginRawAsync()
    {
        using var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("operator", password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (response, ExtractSecret(response));
    }

    private async Task<(LoginResult Result, string Secret)> LoginAsync()
    {
        var (response, secret) = await LoginRawAsync();
        return ((await response.Content.ReadFromJsonAsync<LoginResult>())!, secret);
    }

    private async Task<HttpResponseMessage> RefreshRawAsync(string? secret)
    {
        using var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/identity/refresh");
        if (secret is not null) request.Headers.Add("Cookie", "rt=" + secret);
        return await client.SendAsync(request);
    }

    private async Task<(HttpResponseMessage Response, (LoginResult Result, string Secret))> RefreshAsync(string secret)
    {
        var response = await RefreshRawAsync(secret);
        if (response.StatusCode != HttpStatusCode.OK) return (response, (null!, string.Empty));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (response, ((await response.Content.ReadFromJsonAsync<LoginResult>())!, ExtractSecret(response)));
    }

    private static string ExtractSecret(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rt=", StringComparison.Ordinal));
        return header["rt=".Length..header.IndexOf(';')];
    }

    private static Guid SessionId(string accessToken) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.Single(c => c.Type == "sid").Value);

    // The Secure cookie would not be replayed over the test server's http, so cookies are sent by hand.
    private HttpClient NewClient() => factory!.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private HttpClient Bearer(string token)
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.ExecuteSqlRawAsync(sql);
    }

    private sealed class RefreshFactory(string connection) : WebApplicationFactory<Program>
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
        }
    }
}
