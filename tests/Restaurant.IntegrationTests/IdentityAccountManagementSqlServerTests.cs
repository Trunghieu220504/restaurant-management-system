using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Identity.Application.Accounts;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>HIE-5.2: account management API on SQL Server. Needs IDENTITY_TEST_SQLSERVER.</summary>
public sealed class IdentityAccountManagementSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantAccountsTest_" + Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, (Guid Id, string Password)> seeded = [];
    private Factory? factory;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        factory = new Factory(new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await BusinessAuditTestSupport.MigrateAsync(scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.GetConnectionString()!);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        foreach (var (name, roles) in new (string, string[])[]
        {
            ("admin1", ["Admin"]), ("admin2", ["Admin"]), ("mgr", ["Manager"]), ("mgr2", ["Manager"]),
            ("stf", ["Staff"]), ("kit", ["Kitchen"]), ("mixed", ["Staff", "Manager"])
        })
        {
            var password = RandomPassword();
            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = name, Email = name + "@example.invalid" };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded);
            Assert.True((await manager.AddToRolesAsync(user, roles)).Succeeded);
            seeded[name] = (user.Id, password);
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
            if (target != databaseName || !target.StartsWith("RestaurantAccountsTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe account management test cleanup target.");
            await db.Database.EnsureDeletedAsync();
        }
        finally { await factory.DisposeAsync(); }
    }

    [SqlServerFact]
    public async Task Anonymous_and_unprivileged_roles_cannot_use_account_management()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await CallAsync(HttpMethod.Get, "/api/identity/accounts", null)).Status);
        foreach (var name in new[] { "stf", "kit" })
        {
            var denied = await CallAsync(HttpMethod.Get, "/api/identity/accounts", await LoginAsync(name));
            Assert.Equal(HttpStatusCode.Forbidden, denied.Status);
        }
    }

    [SqlServerFact]
    public async Task Admin_lists_every_account_and_a_manager_only_subordinates()
    {
        var admin = await LoginAsync("admin1");
        var all = await ListAsync(admin);
        Assert.Equal(7, all.Total);
        Assert.Equal(["admin1", "admin2", "kit", "mgr", "mgr2", "mixed", "stf"], all.Items.Select(x => x.UserName));

        var visible = await ListAsync(await LoginAsync("mgr"));
        // "mixed" holds Manager too, so the whole role set is not subordinate and it stays out of reach.
        Assert.Equal(["kit", "stf"], visible.Items.Select(x => x.UserName));
        Assert.Equal(2, visible.Total);

        Assert.Equal(["mixed", "stf"], (await ListAsync(admin, "?role=Staff")).Items.Select(x => x.UserName));
        Assert.Equal(["mgr", "mgr2"], (await ListAsync(admin, "?search=mgr")).Items.Select(x => x.UserName));
        var page = await ListAsync(admin, "?limit=2&offset=2");
        Assert.Equal(7, page.Total);
        Assert.Equal(["kit", "mgr"], page.Items.Select(x => x.UserName));
        Assert.All((await ListAsync(admin, "?active=true")).Items, x => Assert.True(x.Active));
        Assert.Empty((await ListAsync(admin, "?active=false")).Items);

        foreach (var bad in new[] { "?limit=0", "?limit=201", "?offset=-1", "?role=Boss" })
            Assert.Equal(AccountErrors.InputInvalid, (await CallAsync(HttpMethod.Get, "/api/identity/accounts" + bad, admin)).Code);
    }

    [SqlServerFact]
    public async Task Admin_creates_an_account_that_can_sign_in()
    {
        var admin = await LoginAsync("admin1");
        var password = RandomPassword();
        var created = await CallAsync(HttpMethod.Post, "/api/identity/accounts", admin,
            new { userName = "newcook", email = "newcook@example.invalid", password, roles = new[] { "Kitchen" } });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var account = created.Read<AccountDto>();
        Assert.Equal("/api/identity/accounts/" + account.Id, created.Location);
        Assert.Equal(["Kitchen"], account.Roles);
        Assert.True(account.Active);

        using var client = factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("newcook", password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(["Kitchen"], (await login.Content.ReadFromJsonAsync<LoginResult>())!.Account.Roles);
        Assert.DoesNotContain(password, created.Body);
    }

    [SqlServerFact]
    public async Task Create_rejects_duplicates_weak_passwords_and_malformed_input()
    {
        var admin = await LoginAsync("admin1");
        var ok = RandomPassword();
        async Task<ApiResult> Create(string? userName, string? email, string? password, string[]? roles) =>
            await CallAsync(HttpMethod.Post, "/api/identity/accounts", admin, new { userName, email, password, roles });

        Assert.Equal(AccountErrors.UserNameTaken, (await Create("STF", "other@example.invalid", ok, ["Staff"])).Code);
        Assert.Equal(AccountErrors.EmailTaken, (await Create("other", "stf@example.invalid", ok, ["Staff"])).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await Create("other", "stf@example.invalid", ok, ["Staff"])).Status);
        Assert.Equal(AccountErrors.PasswordInvalid, (await Create("other", "other@example.invalid", "short", ["Staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "not-an-email", ok, ["Staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create(" other", "other@example.invalid", ok, ["Staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create(null, "other@example.invalid", ok, ["Staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", null, ok, ["Staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "other@example.invalid", null, ["Staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "other@example.invalid", ok, null)).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "other@example.invalid", ok, [])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "other@example.invalid", ok, ["Boss"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "other@example.invalid", ok, ["staff"])).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await Create("other", "other@example.invalid", ok, ["Staff", "Staff"])).Code);
        Assert.Equal(7, (await ListAsync(admin)).Total);
    }

    [SqlServerFact]
    public async Task Manager_creates_only_subordinate_accounts()
    {
        var manager = await LoginAsync("mgr");
        async Task<ApiResult> Create(string name, params string[] roles) => await CallAsync(HttpMethod.Post, "/api/identity/accounts",
            manager, new { userName = name, email = name + "@example.invalid", password = RandomPassword(), roles });

        Assert.Equal(HttpStatusCode.Created, (await Create("waiter", "Staff", "Bar")).Status);
        foreach (var roles in new[] { new[] { "Manager" }, ["Admin"], ["Staff", "Manager"] })
        {
            var denied = await Create("climber", roles);
            Assert.Equal(HttpStatusCode.Forbidden, denied.Status);
            Assert.Equal(AccountErrors.RoleNotAllowed, denied.Code);
        }
        Assert.Equal(8, (await ListAsync(await LoginAsync("admin1"))).Total);
    }

    [SqlServerFact]
    public async Task Manager_cannot_view_or_change_admins_managers_or_mixed_accounts()
    {
        var manager = await LoginAsync("mgr");
        var before = (await ListAsync(await LoginAsync("admin1"))).Items.OrderBy(x => x.UserName).ToList();
        foreach (var name in new[] { "admin1", "mgr2", "mixed" })
        {
            var id = seeded[name].Id;
            var url = $"/api/identity/accounts/{id}";
            foreach (var call in new[]
            {
                await CallAsync(HttpMethod.Get, url, manager),
                await CallAsync(HttpMethod.Put, url + "/roles", manager, new { roles = new[] { "Staff" } }),
                await CallAsync(HttpMethod.Post, url + "/deactivate", manager),
                await CallAsync(HttpMethod.Post, url + "/reset-password", manager, new { newPassword = RandomPassword() })
            })
            {
                Assert.Equal(HttpStatusCode.Forbidden, call.Status);
                Assert.Equal(AccountErrors.OutOfScope, call.Code);
            }
        }
        var after = (await ListAsync(await LoginAsync("admin1"))).Items.OrderBy(x => x.UserName).ToList();
        Assert.Equal(before, after, new AccountComparer());
    }

    [SqlServerFact]
    public async Task Manager_changes_subordinate_roles_but_cannot_promote()
    {
        var manager = await LoginAsync("mgr");
        var url = $"/api/identity/accounts/{seeded["stf"].Id}/roles";

        var changed = await CallAsync(HttpMethod.Put, url, manager, new { roles = new[] { "Kitchen", "Bar" } });
        Assert.Equal(HttpStatusCode.OK, changed.Status);
        Assert.Equal(["Bar", "Kitchen"], changed.Read<AccountDto>().Roles);

        foreach (var roles in new[] { new[] { "Manager" }, ["Admin"], ["Staff", "Manager"] })
        {
            var denied = await CallAsync(HttpMethod.Put, url, manager, new { roles });
            Assert.Equal(AccountErrors.RoleNotAllowed, denied.Code);
        }
        var current = await CallAsync(HttpMethod.Get, $"/api/identity/accounts/{seeded["stf"].Id}", manager);
        Assert.Equal(["Bar", "Kitchen"], current.Read<AccountDto>().Roles);
    }

    [SqlServerFact]
    public async Task Nobody_changes_their_own_roles_activation_or_password_through_this_api()
    {
        foreach (var name in new[] { "admin1", "mgr" })
        {
            var token = await LoginAsync(name);
            var url = $"/api/identity/accounts/{seeded[name].Id}";
            foreach (var call in new[]
            {
                await CallAsync(HttpMethod.Put, url + "/roles", token, new { roles = new[] { "Admin", "Manager" } }),
                await CallAsync(HttpMethod.Post, url + "/deactivate", token),
                await CallAsync(HttpMethod.Post, url + "/reset-password", token, new { newPassword = RandomPassword() })
            })
            {
                Assert.Equal(HttpStatusCode.Forbidden, call.Status);
                Assert.Equal(AccountErrors.SelfChange, call.Code);
            }
        }
    }

    [SqlServerFact]
    public async Task Role_changes_reach_the_token_already_in_use_and_a_demoted_admin_loses_account_management()
    {
        var staff = await LoginAsync("stf");
        var admin2 = await LoginAsync("admin2");
        using (var client = Bearer(staff))
        {
            var me = await client.GetFromJsonAsync<CurrentAccount>("/api/identity/me");
            Assert.DoesNotContain("seating.catalog.manage", me!.Permissions);
        }
        var promoted = await CallAsync(HttpMethod.Put, $"/api/identity/accounts/{seeded["stf"].Id}/roles", admin2,
            new { roles = new[] { "Manager" } });
        Assert.Equal(HttpStatusCode.OK, promoted.Status);
        using (var client = Bearer(staff))
        {
            var me = await client.GetFromJsonAsync<CurrentAccount>("/api/identity/me");
            Assert.Equal(["Manager"], me!.Roles);
            Assert.Contains("seating.catalog.manage", me.Permissions);
        }

        var admin1 = await LoginAsync("admin1");
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(HttpMethod.Put, $"/api/identity/accounts/{seeded["admin1"].Id}/roles", admin2,
            new { roles = new[] { "Staff" } })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await CallAsync(HttpMethod.Get, "/api/identity/accounts", admin1)).Status);
    }

    [SqlServerFact]
    public async Task Unknown_accounts_are_404_and_unknown_roles_are_400()
    {
        var admin = await LoginAsync("admin1");
        var missing = $"/api/identity/accounts/{Guid.NewGuid()}";
        Assert.Equal(AccountErrors.NotFound, (await CallAsync(HttpMethod.Get, missing, admin)).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await CallAsync(HttpMethod.Put, missing + "/roles", admin, new { roles = new[] { "Staff" } })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await CallAsync(HttpMethod.Post, missing + "/activate", admin)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await CallAsync(HttpMethod.Post, missing + "/deactivate", admin)).Status);
        Assert.Equal(HttpStatusCode.NotFound,
            (await CallAsync(HttpMethod.Post, missing + "/reset-password", admin, new { newPassword = RandomPassword() })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await CallAsync(HttpMethod.Get, "/api/identity/accounts/not-a-guid", admin)).Status);

        var stf = $"/api/identity/accounts/{seeded["stf"].Id}/roles";
        Assert.Equal(AccountErrors.InputInvalid, (await CallAsync(HttpMethod.Put, stf, admin, new { roles = new[] { "Boss" } })).Code);
        Assert.Equal(AccountErrors.InputInvalid, (await CallAsync(HttpMethod.Put, stf, admin, new { roles = Array.Empty<string>() })).Code);
    }

    [SqlServerFact]
    public async Task Deactivation_ends_every_session_and_blocks_sign_in_until_reactivated()
    {
        var admin = await LoginAsync("admin1");
        var first = await LoginAsync("stf");
        var second = await LoginAsync("stf");
        var url = $"/api/identity/accounts/{seeded["stf"].Id}";

        var off = await CallAsync(HttpMethod.Post, url + "/deactivate", admin);
        Assert.Equal(HttpStatusCode.OK, off.Status);
        Assert.False(off.Read<AccountDto>().Active);
        foreach (var token in new[] { first, second })
            Assert.Equal(HttpStatusCode.Unauthorized, (await CallAsync(HttpMethod.Get, "/api/identity/me", token)).Status);
        using (var client = factory!.CreateClient())
        {
            var login = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("stf", seeded["stf"].Password));
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.Equal(0, await db.Set<LoginSession>().CountAsync(s => s.UserId == seeded["stf"].Id && s.RevokedAt == null));
            Assert.Equal(0, await db.Set<RefreshToken>().CountAsync(t => t.UserId == seeded["stf"].Id && t.RevokedAt == null));
        }
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(HttpMethod.Post, url + "/deactivate", admin)).Status); // repeating is harmless

        var on = await CallAsync(HttpMethod.Post, url + "/activate", admin);
        Assert.True(on.Read<AccountDto>().Active);
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(HttpMethod.Get, "/api/identity/me", await LoginAsync("stf"))).Status);
        // Sessions that ended stay ended.
        Assert.Equal(HttpStatusCode.Unauthorized, (await CallAsync(HttpMethod.Get, "/api/identity/me", first)).Status);
    }

    [SqlServerFact]
    public async Task Password_reset_replaces_the_password_ends_sessions_and_clears_lockout()
    {
        var admin = await LoginAsync("admin1");
        var oldToken = await LoginAsync("stf");
        using (var client = factory!.CreateClient())
            for (var i = 0; i < 5; i++)
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("stf", RandomPassword()))).StatusCode);
        using (var client = factory.CreateClient())
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("stf", seeded["stf"].Password))).StatusCode);
        var listed = (await ListAsync(admin, "?search=stf")).Items.Single(x => x.UserName == "stf");
        Assert.True(listed.LockedOut);

        var replacement = RandomPassword();
        var reset = await CallAsync(HttpMethod.Post, $"/api/identity/accounts/{seeded["stf"].Id}/reset-password", admin,
            new { newPassword = replacement });
        Assert.Equal(HttpStatusCode.NoContent, reset.Status);
        Assert.DoesNotContain(replacement, reset.Body);
        Assert.False((await ListAsync(admin, "?search=stf")).Items.Single(x => x.UserName == "stf").LockedOut);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CallAsync(HttpMethod.Get, "/api/identity/me", oldToken)).Status);
        using var again = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await again.PostAsJsonAsync("/api/identity/login", new LoginRequest("stf", replacement))).StatusCode);
    }

    [SqlServerFact]
    public async Task Weak_replacement_password_is_rejected_and_nothing_changes()
    {
        var admin = await LoginAsync("admin1");
        var token = await LoginAsync("stf");
        var weak = await CallAsync(HttpMethod.Post, $"/api/identity/accounts/{seeded["stf"].Id}/reset-password", admin,
            new { newPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.Status);
        Assert.Equal(AccountErrors.PasswordInvalid, weak.Code);
        Assert.Equal(HttpStatusCode.OK, (await CallAsync(HttpMethod.Get, "/api/identity/me", token)).Status);
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/identity/login", new LoginRequest("stf", seeded["stf"].Password))).StatusCode);
    }

    [SqlServerFact]
    public async Task Two_admins_disabling_each_other_at_once_leave_one_active_admin()
    {
        var token1 = await LoginAsync("admin1");
        var token2 = await LoginAsync("admin2");
        var results = await Task.WhenAll(
            CallAsync(HttpMethod.Post, $"/api/identity/accounts/{seeded["admin2"].Id}/deactivate", token1),
            CallAsync(HttpMethod.Post, $"/api/identity/accounts/{seeded["admin1"].Id}/deactivate", token2));

        Assert.Equal(1, results.Count(x => x.Status == HttpStatusCode.OK));
        Assert.Single(results, x => x.Status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        await using var scope = factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var admins = await (from link in db.UserRoles
                            join role in db.Roles on link.RoleId equals role.Id
                            join user in db.Users on link.UserId equals user.Id
                            where role.Name == "Admin" && EF.Property<bool>(user, "IsActive")
                            select user.Id).ToListAsync();
        Assert.Single(admins);
    }

    private async Task<string> LoginAsync(string name)
    {
        using var client = factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest(name, seeded[name].Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResult>())!.AccessToken;
    }

    private HttpClient Bearer(string token)
    {
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<AccountPage> ListAsync(string token, string query = "")
    {
        var result = await CallAsync(HttpMethod.Get, "/api/identity/accounts" + query, token);
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return result.Read<AccountPage>();
    }

    private async Task<ApiResult> CallAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var client = factory!.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        string? code = null;
        if (text.StartsWith('{'))
        {
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("code", out var value)) code = value.GetString();
        }
        return new ApiResult(response.StatusCode, code, text, response.Headers.Location?.ToString());
    }

    private static string RandomPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";

    private sealed record ApiResult(HttpStatusCode Status, string? Code, string Body, string? Location)
    {
        public T Read<T>() => JsonSerializer.Deserialize<T>(Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private sealed class AccountComparer : IEqualityComparer<AccountDto>
    {
        public bool Equals(AccountDto? x, AccountDto? y) => x is not null && y is not null && x.Id == y.Id &&
            x.UserName == y.UserName && x.Active == y.Active && x.Roles.SequenceEqual(y.Roles);

        public int GetHashCode(AccountDto obj) => obj.Id.GetHashCode();
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
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
