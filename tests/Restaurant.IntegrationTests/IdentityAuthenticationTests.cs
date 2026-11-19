using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class IdentityAuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_rejects_invalid_input_without_accessing_database()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new { userName = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("GET", "/api/identity/me")]
    [InlineData("POST", "/api/identity/logout")]
    [InlineData("POST", "/api/identity/change-password")]
    public async Task Account_endpoints_require_authentication(string method, string path)
    {
        using var client = factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Model_has_database_sessions_and_enabled_account_flag()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("[identity].[LoginSession]", sql);
        Assert.Contains("[identity].[RefreshToken]", sql);
        Assert.Contains("[IsActive] bit NOT NULL", sql);
        Assert.Contains("[SecurityStamp]", sql);
        Assert.Equal(3, db.Database.GetMigrations().Count());
    }

    [Fact]
    public async Task Oversized_credentials_are_rejected_before_database_access()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new { userName = "admin", password = new string('x', 20000) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
