using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restaurant.Api.Configuration;
using Restaurant.Modules.Identity.Application.Authorization;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PermissionEndpointTests
{
    [Fact]
    public async Task Seeded_role_matrix_returns_401_403_or_200_for_every_permission()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(ApiFactory.ConnectionString).Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var roles = model.FindEntityType(typeof(IdentityRole<Guid>))!.GetSeedData()
            .ToDictionary(row => (Guid)row["Id"]!, row => (string)row["Name"]!);
        var grants = model.FindEntityType(typeof(RolePermission))!.GetSeedData()
            .GroupBy(row => roles[(Guid)row["RoleId"]!])
            .ToDictionary(group => group.Key, group => group.Select(row => (string)row["PermissionCode"]!).ToArray());
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "restaurant-tests",
            ["Jwt:Audience"] = "restaurant-tests-client",
            ["Jwt:SigningKey"] = ApiFactory.SigningKey
        });
        builder.Services.AddApiAuthentication(builder.Configuration);
        builder.Services.AddSingleton(new TestGrants(grants));
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Matrix";
            options.DefaultChallengeScheme = "Matrix";
            options.DefaultForbidScheme = "Matrix";
        }).AddScheme<AuthenticationSchemeOptions, MatrixAuthentication>("Matrix", _ => { });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        foreach (var code in PermissionCodes.All)
            app.MapGet("/sample/" + code, () => Results.Ok()).RequirePermission(code);
        Assert.Throws<ArgumentException>(() => app.MapGet("/invalid", () => Results.Ok()).RequirePermission("unknown.permission"));
        await app.StartAsync();
        using var client = app.GetTestClient();
        foreach (var code in PermissionCodes.All)
        {
            using var anonymous = await client.GetAsync("/sample/" + code);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            foreach (var role in new[] { "Admin", "Manager", "Staff", "Kitchen", "Bar" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/sample/" + code);
                request.Headers.Add("Test-Role", role);
                using var response = await client.SendAsync(request);
                var expected = grants.GetValueOrDefault(role, []).Contains(code)
                    ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                Assert.True(response.StatusCode == expected, $"{role} / {code}: expected {expected}, got {response.StatusCode}");
            }
        }
    }

    private sealed record TestGrants(Dictionary<string, string[]> Permissions);

    private sealed class MatrixAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, TestGrants grants)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["Test-Role"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim("role", role) }.Concat(
                grants.Permissions.GetValueOrDefault(role, []).Select(code => new Claim("permission", code)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
