using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Restaurant.Api.Configuration;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class HostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_does_not_require_database_or_authentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Readiness_fails_when_database_is_unreachable()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_missing_token()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/system/modules")).StatusCode);
    }

    [Theory]
    [InlineData("Staff", "restaurant-tests", "restaurant-tests-client", false, HttpStatusCode.Forbidden)]
    [InlineData("Admin", "wrong-issuer", "restaurant-tests-client", false, HttpStatusCode.Unauthorized)]
    [InlineData("Admin", "restaurant-tests", "wrong-audience", false, HttpStatusCode.Unauthorized)]
    [InlineData("Admin", "restaurant-tests", "restaurant-tests-client", true, HttpStatusCode.Unauthorized)]
    public async Task Authentication_and_role_are_enforced(
        string role, string issuer, string audience, bool expired, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CreateToken(role, issuer, audience, expired));
        Assert.Equal(expected, (await client.GetAsync("/api/system/modules")).StatusCode);
    }

    [Fact]
    public async Task Valid_admin_can_read_all_registered_modules()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Admin"));
        using var response = await client.GetAsync("/api/system/modules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modules = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(modules);
        Assert.Equal(10, modules.Length);
        Assert.Contains("Inventory", modules);
    }

    [Fact]
    public async Task Invalid_signature_is_rejected()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CreateToken("Admin", signingKey: "another-test-key-that-is-at-least-32-bytes"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/system/modules")).StatusCode);
    }

    [Fact]
    public void Empty_jwt_key_fails_validation()
    {
        var services = new ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience"
            }).Build();
        services.AddLogging();
        services.AddApiAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtSettings>>().Value);
    }

    [Fact]
    public async Task Openapi_is_not_exposed_outside_development()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("Admin"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task Development_exposes_valid_openapi_document()
    {
        using var developmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = developmentFactory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/system/modules", out _));
    }

    private static string CreateToken(string role, string issuer = "restaurant-tests",
        string audience = "restaurant-tests-client", bool expired = false,
        string signingKey = ApiFactory.SigningKey)
    {
        var token = new JwtSecurityToken(
            issuer, audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", role)],
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(expired ? -5 : 5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
