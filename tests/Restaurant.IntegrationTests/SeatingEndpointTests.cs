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
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Presentation;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>
/// HIE-2.1 HTTP contract with the real Seating endpoints and stubbed services: who may call what (using the
/// seeded role permissions), the Idempotency-Key requirement, status codes and the stable error code.
/// </summary>
public sealed class SeatingEndpointTests
{
    private static readonly string[] Roles = ["Admin", "Manager", "Staff", "Kitchen", "Bar"];
    private const string Catalog = "seating.catalog.manage";
    private const string Sessions = "seating.sessions.manage";

    private static readonly (string Name, HttpMethod Method, string Url, string Permission, bool NeedsKey, object? Body)[] Routes =
    [
        ("list areas", HttpMethod.Get, "/api/seating/areas", Catalog, false, null),
        ("create area", HttpMethod.Post, "/api/seating/areas", Catalog, false, new { code = "A", name = "Area" }),
        ("update area", HttpMethod.Put, "/api/seating/areas/1", Catalog, false, new { name = "Area", version = "AAAAAAAAAAE=" }),
        ("archive area", HttpMethod.Post, "/api/seating/areas/1/archive", Catalog, false, new { version = "AAAAAAAAAAE=" }),
        ("list tables", HttpMethod.Get, "/api/seating/tables", Catalog, false, null),
        ("create table", HttpMethod.Post, "/api/seating/tables", Catalog, false, new { areaId = 1, code = "T", name = "Table" }),
        ("update table", HttpMethod.Put, "/api/seating/tables/1", Catalog, false, new { areaId = 1, name = "Table", version = "AAAAAAAAAAE=" }),
        ("archive table", HttpMethod.Post, "/api/seating/tables/1/archive", Catalog, false, new { version = "AAAAAAAAAAE=" }),
        ("board", HttpMethod.Get, "/api/seating/board", Sessions, false, null),
        ("open session", HttpMethod.Post, "/api/seating/tables/1/sessions", Sessions, true, null),
        ("transfer session", HttpMethod.Post, "/api/seating/sessions/1/transfer", Sessions, true, new { toTableId = 2, reason = "Move" }),
        ("close session", HttpMethod.Post, "/api/seating/sessions/1/close", Sessions, true, null),
    ];

    [Fact]
    public async Task Every_route_returns_401_without_login_and_403_without_its_permission()
    {
        var grants = ReadSeededGrants();
        Assert.Contains(Sessions, grants["Staff"]);
        Assert.DoesNotContain(Catalog, grants["Staff"]);
        await using var app = await StartAsync(new StubServices(), grants);
        using var client = app.GetTestClient();

        foreach (var route in Routes)
        {
            using var anonymous = await client.SendAsync(Build(route, role: null, key: Guid.NewGuid()));
            Assert.True(anonymous.StatusCode == HttpStatusCode.Unauthorized, $"{route.Name}: anonymous got {anonymous.StatusCode}");
            foreach (var role in Roles)
            {
                using var response = await client.SendAsync(Build(route, role, Guid.NewGuid()));
                var allowed = grants.GetValueOrDefault(role, []).Contains(route.Permission);
                if (allowed)
                    Assert.True(response.IsSuccessStatusCode, $"{route.Name} / {role}: expected success, got {response.StatusCode}");
                else
                    Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{route.Name} / {role}: expected 403, got {response.StatusCode}");
            }
        }
    }

    [Fact]
    public async Task Session_commands_require_a_valid_idempotency_key()
    {
        await using var app = await StartAsync(new StubServices(), ReadSeededGrants());
        using var client = app.GetTestClient();
        foreach (var route in Routes.Where(r => r.NeedsKey))
        {
            foreach (var bad in new string?[] { null, "", "not-a-guid", Guid.Empty.ToString() })
            {
                using var request = Build(route, "Staff", key: null);
                if (bad is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", bad);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(SeatingErrors.IdempotencyRequired, await ReadCodeAsync(response));
            }
        }
    }

    [Fact]
    public async Task Creates_return_201_replays_200_and_errors_map_to_stable_status_and_code()
    {
        var stub = new StubServices();
        await using var app = await StartAsync(stub, ReadSeededGrants());
        using var client = app.GetTestClient();
        var create = Routes.Single(r => r.Name == "create area");
        var open = Routes.Single(r => r.Name == "open session");

        using var created = await client.SendAsync(Build(create, "Manager", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/seating/areas/7", created.Headers.Location?.OriginalString);

        using var first = await client.SendAsync(Build(open, "Staff", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        stub.Replay = true;
        using var replay = await client.SendAsync(Build(open, "Staff", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        stub.Replay = false;

        var expected = new (string Code, HttpStatusCode Status)[]
        {
            (SeatingErrors.InputInvalid, HttpStatusCode.BadRequest),
            (SeatingErrors.TransferSameTable, HttpStatusCode.BadRequest),
            (SeatingErrors.TableNotFound, HttpStatusCode.NotFound),
            (SeatingErrors.SessionNotFound, HttpStatusCode.NotFound),
            (SeatingErrors.TableOccupied, HttpStatusCode.Conflict),
            (SeatingErrors.SessionClosed, HttpStatusCode.Conflict),
            (SeatingErrors.SessionHasOpenOrders, HttpStatusCode.Conflict),
            (SeatingErrors.ConcurrencyConflict, HttpStatusCode.Conflict),
            (SeatingErrors.IdempotencyConflict, HttpStatusCode.Conflict),
            (SeatingErrors.DayNotOpen, HttpStatusCode.Conflict),
        };
        foreach (var (code, status) in expected)
        {
            stub.Error = code;
            using var response = await client.SendAsync(Build(open, "Staff", Guid.NewGuid()));
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(code, await ReadCodeAsync(response));
        }
    }

    [Fact]
    public async Task Every_error_code_is_lowercase_dotted_and_unique()
    {
        var codes = typeof(SeatingErrors).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*){2}$", code));
        Assert.All(codes.Where(c => c != SeatingErrors.DayNotOpen), code => Assert.StartsWith("seating.", code));
        await Task.CompletedTask;
    }

    private static HttpRequestMessage Build(
        (string Name, HttpMethod Method, string Url, string Permission, bool NeedsKey, object? Body) route, string? role, Guid? key)
    {
        var request = new HttpRequestMessage(route.Method, route.Url);
        if (route.Body is not null) request.Content = JsonContent.Create(route.Body);
        if (role is not null) request.Headers.Add("Test-Role", role);
        if (key is not null) request.Headers.Add("Idempotency-Key", key.Value.ToString());
        return request;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static Dictionary<string, string[]> ReadSeededGrants()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(ApiFactory.ConnectionString).Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var roles = model.FindEntityType(typeof(IdentityRole<Guid>))!.GetSeedData()
            .ToDictionary(row => (Guid)row["Id"]!, row => (string)row["Name"]!);
        return model.FindEntityType(typeof(RolePermission))!.GetSeedData()
            .GroupBy(row => roles[(Guid)row["RoleId"]!])
            .ToDictionary(group => group.Key, group => group.Select(row => (string)row["PermissionCode"]!).ToArray());
    }

    private static async Task<WebApplication> StartAsync(StubServices stub, Dictionary<string, string[]> grants)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new TestGrants(grants));
        builder.Services.AddSingleton<ISeatingCatalog>(stub);
        builder.Services.AddSingleton<ISeatingSessions>(stub);
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Matrix";
            options.DefaultChallengeScheme = "Matrix";
            options.DefaultForbidScheme = "Matrix";
        }).AddScheme<AuthenticationSchemeOptions, MatrixAuthentication>("Matrix", _ => { });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSeatingEndpoints();
        await app.StartAsync();
        return app;
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
            var claims = new[] { new Claim("role", role), new Claim("sub", "44444444-4444-4444-4444-444444444444") }
                .Concat(grants.Permissions.GetValueOrDefault(role, []).Select(code => new Claim("permission", code)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    /// <summary>Returns a fixed success, or the configured error, for every call.</summary>
    private sealed class StubServices : ISeatingCatalog, ISeatingSessions
    {
        public string? Error { get; set; }
        public bool Replay { get; set; }

        private SeatingResult<T> Result<T>(T value) =>
            Error is null ? SeatingResult<T>.Ok(value, Replay) : SeatingResult<T>.Fail(Error);

        private static readonly AreaDto Area = new(7, "A", "Area", true, "AAAAAAAAAAE=");
        private static readonly TableDto Table = new(3, 7, "T", "Table", true, "locator", "AAAAAAAAAAE=");
        private static readonly SessionDto Session = new(5, 3, Guid.Empty, DateTimeOffset.UnixEpoch, Guid.Empty, null, null, "AAAAAAAAAAE=");

        public Task<IReadOnlyList<AreaDto>> ListAreasAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AreaDto>>([Area]);
        public Task<SeatingResult<AreaDto>> CreateAreaAsync(CreateAreaRequest request, CancellationToken ct) => Task.FromResult(Result(Area));
        public Task<SeatingResult<AreaDto>> UpdateAreaAsync(long id, UpdateAreaRequest request, CancellationToken ct) => Task.FromResult(Result(Area));
        public Task<SeatingResult<AreaDto>> ArchiveAreaAsync(long id, ArchiveRequest request, CancellationToken ct) => Task.FromResult(Result(Area));
        public Task<IReadOnlyList<TableDto>> ListTablesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<TableDto>>([Table]);
        public Task<SeatingResult<TableDto>> CreateTableAsync(CreateTableRequest request, CancellationToken ct) => Task.FromResult(Result(Table));
        public Task<SeatingResult<TableDto>> UpdateTableAsync(long id, UpdateTableRequest request, CancellationToken ct) => Task.FromResult(Result(Table));
        public Task<SeatingResult<TableDto>> ArchiveTableAsync(long id, ArchiveRequest request, CancellationToken ct) => Task.FromResult(Result(Table));
        public Task<IReadOnlyList<BoardAreaDto>> BoardAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<BoardAreaDto>>([]);
        public Task<SeatingResult<SessionDto>> OpenAsync(long tableId, Guid actor, Guid key, CancellationToken ct) => Task.FromResult(Result(Session));
        public Task<SeatingResult<SessionDto>> TransferAsync(long sessionId, TransferRequest request, Guid actor, Guid key, CancellationToken ct) => Task.FromResult(Result(Session));
        public Task<SeatingResult<SessionDto>> CloseAsync(long sessionId, Guid actor, Guid key, CancellationToken ct) => Task.FromResult(Result(Session));
    }
}
