using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Seating.Application;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class GuestMenuAccessTests
{
    [Theory]
    [InlineData("Bearer staff-token", "qr.token.missing")]
    [InlineData("Guest invalid", "qr.token.invalid")]
    [InlineData("Guest expired", "qr.token.expired")]
    [InlineData("Guest revoked", "qr.token.revoked")]
    public async Task Menu_rejects_invalid_supplied_guest_access_before_reading_catalog(string? header, string code)
    {
        using var original = new ApiFactory();
        var menu = new MenuStub();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IGuestAccess>(new AccessStub());
            services.AddSingleton<IMenuAvailability>(menu);
        }));
        using var client = factory.CreateClient();
        if (header is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);
        using var response = await client.GetAsync("/api/public/menu");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, menu.Reads);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task Valid_guest_token_can_read_menu_without_receiving_staff_privileges()
    {
        using var original = new ApiFactory();
        var menu = new MenuStub();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IGuestAccess>(new AccessStub());
            services.AddSingleton<IMenuAvailability>(menu);
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Guest valid");
        using var response = await client.GetAsync("/api/public/menu");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, menu.Reads);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var staff = await client.GetAsync("/api/catalog/menu");
        Assert.Equal(HttpStatusCode.Unauthorized, staff.StatusCode);
        client.DefaultRequestHeaders.Remove("Authorization");
        using var anonymousMenu = await client.GetAsync("/api/public/menu");
        Assert.Equal(HttpStatusCode.OK, anonymousMenu.StatusCode);
        using var anonymousSession = await client.GetAsync("/api/public/guest-access/session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSession.StatusCode);
    }

    [Theory]
    [InlineData("/api/public/menu/")]
    [InlineData("/API/PUBLIC/MENU")]
    [InlineData("/api/public/menu?tableCode=qr-1")]
    public async Task Alternate_route_spelling_or_table_locator_cannot_bypass_token_validation(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Guest invalid");
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rotating_guest_headers_cannot_bypass_the_read_rate_limit()
    {
        using var original = new ApiFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IGuestAccess>(new AccessStub())));
        using var client = factory.CreateClient();
        for (var i = 0; i < 240; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/menu");
            request.Headers.TryAddWithoutValidation("Authorization", "Guest " + i.ToString().PadLeft(43, 'x'));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var limited = await client.GetAsync("/api/public/menu");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());
        Assert.Equal("qr.rate.limited", problem.RootElement.GetProperty("code").GetString());
    }

    private sealed class AccessStub : IGuestAccess
    {
        public Task<SeatingResult<GuestSession>> CheckAsync(string token, CancellationToken ct) => Task.FromResult(token == "valid"
            ? SeatingResult<GuestSession>.Ok(new("Table 1", DateTimeOffset.UtcNow.AddHours(1)))
            : SeatingResult<GuestSession>.Fail("qr.token." + (token is "expired" or "revoked" ? token : "invalid")));
        public Task<SeatingResult<GuestInvite>> InviteAsync(long session, Guid actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeatingResult<GuestToken>> ExchangeAsync(GuestExchange request, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class MenuStub : IMenuAvailability
    {
        public int Reads { get; private set; }
        public Task<PublicMenu> PublicAsync(CancellationToken ct) { Reads++; return Task.FromResult(new PublicMenu(Guid.NewGuid(), [])); }
        public Task<AvailabilitySnapshot> SnapshotAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> SetAsync(AvailabilityChange change, Guid actor, CancellationToken ct) => throw new NotSupportedException();
    }
}
