using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Presentation;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class CatalogEndpointTests
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private const string Version = "AAAAAAAAAAE=";
    private static readonly (HttpMethod Method, string Path, object? Body)[] Mutations =
    [
        (HttpMethod.Post, "/groups", new { name = "Nhóm", sortOrder = 0 }),
        (HttpMethod.Put, $"/groups/{Id}", new { name = "Nhóm", sortOrder = 0, active = true, version = Version }),
        (HttpMethod.Post, "/items", new { groupId = Id, code = "PHO", name = "Phở", station = "BEP", initialVariant = new { code = "STD", name = "Chuẩn", price = 1000 } }),
        (HttpMethod.Put, $"/items/{Id}", new { groupId = Id, code = "PHO", name = "Phở", station = "BEP", active = true, version = Version }),
        (HttpMethod.Post, $"/items/{Id}/variants", new { code = "L", name = "Lớn", price = 65000 }),
        (HttpMethod.Put, $"/items/{Id}/variants/{Id}", new { code = "L", name = "Lớn", price = 65000, active = true, version = Version }),
        (HttpMethod.Post, "/option-groups", new { name = "Topping", type = "TOPPING" }),
        (HttpMethod.Put, $"/option-groups/{Id}", new { name = "Topping", type = "TOPPING", active = true, version = Version }),
        (HttpMethod.Post, $"/option-groups/{Id}/options", new { name = "Bò", extraPrice = 15000 }),
        (HttpMethod.Put, $"/option-groups/{Id}/options/{Id}", new { name = "Bò", extraPrice = 15000, active = true, version = Version }),
        (HttpMethod.Put, $"/items/{Id}/option-groups/{Id}", new { minSelections = 0, maxSelections = 1 }),
        (HttpMethod.Delete, $"/items/{Id}/option-groups/{Id}", null),
    ];

    [Fact]
    public async Task Every_write_requires_the_seeded_menu_management_permission_and_menu_requires_login()
    {
        var grants = SeededGrants();
        await using var app = await StartAsync(new StubMenu(), grants);
        using var client = app.GetTestClient();
        foreach (var mutation in Mutations)
        {
            using var anonymous = await client.SendAsync(Request(mutation, null));
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            foreach (var role in new[] { "Admin", "Manager", "Staff", "Kitchen", "Bar" })
            {
                using var response = await client.SendAsync(Request(mutation, role));
                var allowed = grants.GetValueOrDefault(role, []).Contains("catalog.menu.manage");
                if (allowed) Assert.True(response.IsSuccessStatusCode, $"{role} {mutation.Path}: {response.StatusCode}");
                else Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
        using var noLogin = await client.GetAsync("/api/catalog/menu");
        Assert.Equal(HttpStatusCode.Unauthorized, noLogin.StatusCode);
        using var employee = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/menu");
        employee.Headers.Add("Test-Role", "Staff");
        using var menu = await client.SendAsync(employee);
        Assert.Equal(HttpStatusCode.OK, menu.StatusCode);
        var body = await menu.Content.ReadFromJsonAsync<MenuDto>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task Create_locations_delete_status_and_problem_codes_follow_the_contract()
    {
        var stub = new StubMenu();
        await using var app = await StartAsync(stub, SeededGrants());
        using var client = app.GetTestClient();
        using var create = await client.SendAsync(Request(Mutations[0], "Manager"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal($"/api/catalog/groups/{Id}", create.Headers.Location?.OriginalString);
        using var removed = await client.SendAsync(Request(Mutations[^1], "Manager"));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        foreach (var (code, status) in new[]
        {
            (CatalogErrors.InputInvalid, HttpStatusCode.BadRequest),
            (CatalogErrors.SelectionRangeInvalid, HttpStatusCode.BadRequest),
            (CatalogErrors.ItemNotFound, HttpStatusCode.NotFound),
            (CatalogErrors.OptionNotFound, HttpStatusCode.NotFound),
            (CatalogErrors.ItemCodeConflict, HttpStatusCode.Conflict),
            (CatalogErrors.ConcurrencyConflict, HttpStatusCode.Conflict),
            (CatalogErrors.OptionRequired, HttpStatusCode.Conflict)
        })
        {
            stub.Error = code;
            using var response = await client.SendAsync(Request(Mutations[0], "Manager"));
            Assert.Equal(status, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData(-1, 1, 2, false)]
    [InlineData(2, 1, 2, false)]
    [InlineData(0, 0, 1, false)]
    [InlineData(0, 1, 0, false)]
    [InlineData(0, 3, 2, false)]
    [InlineData(0, 1, 1, true)]
    [InlineData(2, 2, 2, true)]
    public void Selection_limits_require_available_options(int min, int max, int count, bool valid) =>
        Assert.Equal(valid, CatalogRules.ValidSelections(min, max, count));

    [Fact]
    public void Input_rules_reject_unsafe_money_codes_and_versions()
    {
        Assert.False(CatalogRules.ValidPrice(-1));
        Assert.False(CatalogRules.ValidPrice(CatalogRules.MaximumPrice + 1));
        Assert.False(CatalogRules.ValidCode("Bad code"));
        Assert.False(CatalogRules.ValidCode(null));
        Assert.False(CatalogRules.ValidName("   "));
        Assert.False(CatalogRules.ValidName(new string('x', 201)));
        Assert.False(CatalogRules.TryVersion("not-a-version", out _));
        Assert.False(CatalogRules.TryVersion("AQ==", out _));
        Assert.True(CatalogRules.TryVersion(Version, out _));
        Assert.True(CatalogRules.ValidName("  Phở bò  "));
        Assert.True(CatalogRules.ValidCode(" PHO_01 "));
    }

    private static HttpRequestMessage Request((HttpMethod Method, string Path, object? Body) route, string? role)
    {
        var request = new HttpRequestMessage(route.Method, "/api/catalog" + route.Path);
        if (route.Body is not null) request.Content = JsonContent.Create(route.Body);
        if (role is not null) request.Headers.Add("Test-Role", role);
        return request;
    }

    private static Dictionary<string, string[]> SeededGrants()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(ApiFactory.ConnectionString).Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var roles = model.FindEntityType(typeof(IdentityRole<Guid>))!.GetSeedData().ToDictionary(x => (Guid)x["Id"]!, x => (string)x["Name"]!);
        return model.FindEntityType(typeof(RolePermission))!.GetSeedData().GroupBy(x => roles[(Guid)x["RoleId"]!])
            .ToDictionary(x => x.Key, x => x.Select(row => (string)row["PermissionCode"]!).ToArray());
    }

    private static async Task<WebApplication> StartAsync(StubMenu stub, Dictionary<string, string[]> grants)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(grants);
        builder.Services.AddSingleton<ICatalogMenu>(stub);
        builder.Services.AddAuthentication("CatalogTest").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("CatalogTest", _ => { });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCatalogEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, Dictionary<string, string[]> grants)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["Test-Role"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim("role", role) }.Concat(grants.GetValueOrDefault(role, []).Select(x => new Claim("permission", x)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class StubMenu : ICatalogMenu
    {
        public string? Error { get; set; }
        private static readonly Guid Key = Guid.Parse(Id);
        private static readonly MenuGroupDto Group = new(Key, "Nhóm", 0, true, Version);
        private static readonly MenuItemDto Item = new(Key, Key, "PHO", "Phở", "BEP", null, true, Version, [], []);
        private static readonly VariantDto Variant = new(Key, Key, "L", "Lớn", 65000, true, Version);
        private static readonly OptionGroupDto Options = new(Key, "Topping", "TOPPING", true, Version, []);
        private static readonly OptionDto Option = new(Key, Key, "Bò", 15000, true, Version);
        private Task<CatalogResult<T>> Result<T>(T value) => Task.FromResult(Error is null ? CatalogResult<T>.Ok(value) : CatalogResult<T>.Fail(Error));
        public Task<MenuDto> GetMenuAsync(CancellationToken ct) => Task.FromResult(new MenuDto([Group], [Item], [Options]));
        public Task<CatalogResult<MenuGroupDto>> CreateGroupAsync(CreateMenuGroupRequest r, CancellationToken ct) => Result(Group);
        public Task<CatalogResult<MenuGroupDto>> UpdateGroupAsync(Guid id, UpdateMenuGroupRequest r, CancellationToken ct) => Result(Group);
        public Task<CatalogResult<MenuItemDto>> CreateItemAsync(CreateMenuItemRequest r, CancellationToken ct) => Result(Item);
        public Task<CatalogResult<MenuItemDto>> UpdateItemAsync(Guid id, UpdateMenuItemRequest r, CancellationToken ct) => Result(Item);
        public Task<CatalogResult<VariantDto>> CreateVariantAsync(Guid itemId, CreateVariantRequest r, CancellationToken ct) => Result(Variant);
        public Task<CatalogResult<VariantDto>> UpdateVariantAsync(Guid itemId, Guid id, UpdateVariantRequest r, CancellationToken ct) => Result(Variant);
        public Task<CatalogResult<OptionGroupDto>> CreateOptionGroupAsync(CreateOptionGroupRequest r, CancellationToken ct) => Result(Options);
        public Task<CatalogResult<OptionGroupDto>> UpdateOptionGroupAsync(Guid id, UpdateOptionGroupRequest r, CancellationToken ct) => Result(Options);
        public Task<CatalogResult<OptionDto>> CreateOptionAsync(Guid groupId, CreateOptionRequest r, CancellationToken ct) => Result(Option);
        public Task<CatalogResult<OptionDto>> UpdateOptionAsync(Guid groupId, Guid id, UpdateOptionRequest r, CancellationToken ct) => Result(Option);
        public Task<CatalogResult<ItemOptionGroupDto>> AssignOptionGroupAsync(Guid itemId, Guid groupId, AssignOptionGroupRequest r, CancellationToken ct) => Result(new ItemOptionGroupDto(Key, 0, 1));
        public Task<CatalogResult<bool>> RemoveOptionGroupAsync(Guid itemId, Guid groupId, CancellationToken ct) => Result(true);
    }
}
