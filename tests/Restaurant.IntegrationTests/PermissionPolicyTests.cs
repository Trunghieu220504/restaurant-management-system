using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Configuration;
using Restaurant.Modules.Identity.Application.Authorization;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PermissionPolicyTests
{
    [Fact]
    public async Task Every_seeded_permission_has_an_authenticated_claim_policy()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(ApiFactory.ConnectionString).Options);
        var codes = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Permission))!.GetSeedData()
            .Select(row => (string)row["Code"]!).ToArray();
        Assert.Equal(26, codes.Length);
        Assert.Equal(codes.Order(), PermissionCodes.All.Order());
        Assert.Equal(26, PermissionCodes.All.Distinct(StringComparer.Ordinal).Count());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiAuthentication(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        foreach (var code in codes)
        {
            var policy = await policies.GetPolicyAsync(code);
            Assert.NotNull(policy);
            var granted = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", code)], "test"));
            Assert.True((await authorization.AuthorizeAsync(granted, null, policy)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", code)])), null, policy)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "Admin")], "test")), null, policy)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "unknown.permission")], "test")), null, policy)).Succeeded);
        }
    }
}
