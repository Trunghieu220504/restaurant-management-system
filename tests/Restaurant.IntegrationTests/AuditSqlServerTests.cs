using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Modules.Audit.Application;
using Restaurant.Modules.Audit.Contracts;
using Restaurant.Modules.Audit.Domain;
using Restaurant.Modules.Audit.Infrastructure;
using Restaurant.Modules.Audit.Infrastructure.Persistence;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class AuditSqlServerTests(AuditSqlServerFixture fixture) : IClassFixture<AuditSqlServerFixture>
{
    private static readonly DateTimeOffset Occurred = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);

    private static AuditEntry Message(Guid? actor = null, DateTimeOffset? occurred = null) =>
        new(Guid.NewGuid(), occurred ?? Occurred, actor ?? Guid.NewGuid(), "Sales", "sales.batch.confirmed",
            "Order", Guid.NewGuid().ToString("D"), Guid.NewGuid(), "Xác nhận món: phở bò, thêm quẩy");

    [SqlServerFact]
    public async Task Unicode_roundtrip_uses_server_recording_time_and_exact_retry_does_not_overwrite()
    {
        var entry = Message();
        var recorded = Occurred.AddDays(2);
        await using var db = fixture.Database();
        var sink = new AuditLog(db, new FixedClock(recorded));
        await sink.AppendAsync(entry);
        await new AuditLog(db, new FixedClock(recorded.AddDays(1))).AppendAsync(entry);
        var stored = await db.Entries.AsNoTracking().SingleAsync(x => x.EventId == entry.EventId);
        Assert.Equal(recorded, stored.RecordedAtUtc);
        Assert.Equal(entry.Reason, stored.Reason);
        Assert.Equal(entry.OccurredAtUtc, stored.OccurredAtUtc);
        Assert.Equal(1, await db.Entries.CountAsync(x => x.EventId == entry.EventId));
        foreach (var conflict in new[] { entry with { Reason = "Khác" }, entry with { ActorId = Guid.NewGuid() },
            entry with { OccurredAtUtc = entry.OccurredAtUtc.AddTicks(1) }, entry with { EntityId = "other" } })
            await Assert.ThrowsAsync<AuditEventConflictException>(() => sink.AppendAsync(conflict));
        Assert.Equal(entry.Reason, (await db.Entries.AsNoTracking().SingleAsync(x => x.EventId == entry.EventId)).Reason);
    }

    [SqlServerFact]
    public async Task Concurrent_same_event_deliveries_commit_one_row_and_conflicting_payload_cannot_win_again()
    {
        var entry = Message();
        async Task Deliver(AuditEntry input)
        {
            await using var db = fixture.Database();
            await new AuditLog(db, TimeProvider.System).AppendAsync(input);
        }
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Deliver(entry)));
        await using var check = fixture.Database();
        Assert.Equal(1, await check.Entries.CountAsync(x => x.EventId == entry.EventId));
        await Assert.ThrowsAsync<AuditEventConflictException>(() => Deliver(entry with { Action = "different" }));
    }

    [SqlServerFact]
    public async Task Filters_are_combined_time_range_is_half_open_and_pages_have_stable_order()
    {
        var actor = Guid.NewGuid();
        var basis = Message(actor);
        var start = Occurred.AddDays(10);
        var entries = Enumerable.Range(1, 5).Select(i => basis with
        {
            EventId = new Guid($"00000000-0000-0000-0000-{i:000000000000}"),
            OccurredAtUtc = start
        }).ToArray();
        // Distinct actor/entity/time decoys prove AND rather than OR filters.
        var decoys = new[] { basis with { EventId = Guid.NewGuid(), ActorId = Guid.NewGuid(), OccurredAtUtc = start },
            basis with { EventId = Guid.NewGuid(), EntityId = "other", OccurredAtUtc = start },
            basis with { EventId = Guid.NewGuid(), EntityType = "Invoice", OccurredAtUtc = start },
            basis with { EventId = Guid.NewGuid(), OccurredAtUtc = start.AddTicks(-1) },
            basis with { EventId = Guid.NewGuid(), OccurredAtUtc = start.AddHours(1) } };
        await using var db = fixture.Database();
        var sink = new AuditLog(db, TimeProvider.System);
        foreach (var input in entries.Concat(decoys)) await sink.AppendAsync(input);
        var service = new AuditQueries(db);
        var query = new AuditLogQuery(actor, basis.EntityType, basis.EntityId, start, start.AddHours(1), PageSize: 2);
        var first = await service.FindAsync(query);
        var second = await service.FindAsync(query with { Page = 2 });
        var last = await service.FindAsync(query with { Page = 3 });
        var repeated = await service.FindAsync(query);
        Assert.Equal(5, first.Total);
        Assert.Equal(new[] { entries[4].EventId, entries[3].EventId }, first.Items.Select(x => x.EventId));
        Assert.Equal(first.Items.Select(x => x.EventId), repeated.Items.Select(x => x.EventId));
        Assert.Equal(entries.Select(x => x.EventId).Reverse(), first.Items.Concat(second.Items).Concat(last.Items).Select(x => x.EventId));
        Assert.All(first.Items, x => Assert.Equal(basis.Reason, x.Reason));
    }

    [SqlServerFact]
    public async Task Tracked_and_bulk_updates_and_deletes_are_rejected_and_original_is_retained()
    {
        var input = Message();
        await using (var db = fixture.Database())
        {
            await new AuditLog(db, TimeProvider.System).AppendAsync(input);
            var row = await db.Entries.SingleAsync(x => x.EventId == input.EventId);
            db.Entry(row).Property(x => x.Reason).CurrentValue = "modified";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.Entry(row).State = EntityState.Unchanged;
            db.Entries.Remove(row);
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        }
        await using (var db = fixture.Database())
        {
            var update = await Assert.ThrowsAsync<SqlException>(() => db.Entries.Where(x => x.EventId == input.EventId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Reason, "bulk edit")));
            Assert.Equal(51000, update.Number);
            var delete = await Assert.ThrowsAsync<SqlException>(() => db.Entries.Where(x => x.EventId == input.EventId).ExecuteDeleteAsync());
            Assert.Equal(51000, delete.Number);
        }
        await using var check = fixture.Database();
        Assert.Equal(input.Reason, (await check.Entries.SingleAsync(x => x.EventId == input.EventId)).Reason);
    }

    [SqlServerFact]
    public async Task Audit_transaction_rollback_leaves_no_record_and_later_delivery_can_commit()
    {
        var input = Message();
        await using (var db = fixture.Database())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await new AuditLog(db, TimeProvider.System).AppendAsync(input);
            await transaction.RollbackAsync();
        }
        await using var check = fixture.Database();
        Assert.False(await check.Entries.AnyAsync(x => x.EventId == input.EventId));
        await new AuditLog(check, TimeProvider.System).AppendAsync(input);
        Assert.True(await check.Entries.AnyAsync(x => x.EventId == input.EventId));
        // This verifies the sink's transaction only, not Sales/Billing atomicity.
    }

    [SqlServerFact]
    public async Task Idempotent_migration_script_runs_twice_preserves_data_and_keeps_trigger()
    {
        var input = Message();
        await using var db = fixture.Database();
        await new AuditLog(db, TimeProvider.System).AppendAsync(input);
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        await using var connection = new SqlConnection(fixture.Connection);
        await connection.OpenAsync();
        for (var pass = 0; pass < 2; pass++)
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync();
            }
        Assert.Equal(input.Reason, (await db.Entries.AsNoTracking().SingleAsync(x => x.EventId == input.EventId)).Reason);
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        var exception = await Assert.ThrowsAsync<SqlException>(() => db.Entries.Where(x => x.EventId == input.EventId).ExecuteDeleteAsync());
        Assert.Equal(51000, exception.Number);
    }

    [SqlServerFact]
    public async Task Manager_can_read_bounded_payload_but_staff_admin_and_anonymous_cannot()
    {
        var input = Message();
        await using (var db = fixture.Database()) await new AuditLog(db, TimeProvider.System).AppendAsync(input);
        using var anonymous = fixture.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/audit/logs")).StatusCode);
        foreach (var role in new[] { "Manager", "Staff", "Admin" })
        {
            var login = await fixture.LoginAsync(role);
            using var client = fixture.Client(login.Token);
            var response = await client.GetAsync($"/api/audit/logs?actorId={input.ActorId:D}");
            Assert.Equal(role == "Manager" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
            if (role != "Manager") continue;
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            var page = await response.Content.ReadFromJsonAsync<AuditLogPage>();
            var item = Assert.Single(page!.Items);
            Assert.Equal(input.EventId, item.EventId);
            Assert.Equal(input.Reason, item.Reason);
            Assert.Equal(1, page.Page);
            Assert.Equal(20, page.PageSize);
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("accessToken", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/api/audit/logs", input)).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Invalid_query_shapes_return_stable_problem_and_valid_utc_zero_offset_is_accepted()
    {
        var login = await fixture.LoginAsync("Manager");
        using var client = fixture.Client(login.Token);
        foreach (var query in new[] { "page=0", "page=-1", "page=oops", "pageSize=0", "pageSize=101", "page=2147483647&pageSize=100",
            "actorId=bad", "actorId=00000000-0000-0000-0000-000000000000", "actorId=", "entityType=", "entityId=%20",
            "entityId=" + new string('x', 101), "fromUtc=2026-11-19", "fromUtc=2026-11-19T00%3A00%3A00%2B07%3A00",
            "fromUtc=2026-11-20T00%3A00%3A00Z&toUtc=2026-11-19T00%3A00%3A00Z", "page=1&page=2", "unknown=1", "Page=1" })
        {
            var response = await client.GetAsync("/api/audit/logs?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("audit.query.invalid", problem.RootElement.GetProperty("code").GetString());
        }
        foreach (var from in new[] { "2026-11-19T00:00:00Z", "2026-11-19T00:00:00.1234567Z", "2026-11-19T00:00:00+00:00" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/audit/logs?fromUtc=" + Uri.EscapeDataString(from))).StatusCode);
    }

    [SqlServerFact]
    public async Task Revoked_disabled_and_removed_manager_authority_are_enforced_on_existing_tokens()
    {
        var revoked = await fixture.LoginAsync("Manager");
        using var revokedClient = fixture.Client(revoked.Token);
        Assert.Equal(HttpStatusCode.OK, (await revokedClient.GetAsync("/api/audit/logs")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await revokedClient.PostAsync("/api/identity/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await revokedClient.GetAsync("/api/audit/logs")).StatusCode);

        var disabled = await fixture.LoginAsync("Manager");
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == disabled.UserId);
            db.Entry(user).Property<bool>("IsActive").CurrentValue = false;
            await db.SaveChangesAsync();
        }
        using var disabledClient = fixture.Client(disabled.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await disabledClient.GetAsync("/api/audit/logs")).StatusCode);

        var downgraded = await fixture.LoginAsync("Manager");
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(downgraded.UserId.ToString()))!, "Manager")).Succeeded);
        }
        using var downgradedClient = fixture.Client(downgraded.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await downgradedClient.GetAsync("/api/audit/logs")).StatusCode);
    }

    [SqlServerFact]
    public async Task Openapi_documents_only_read_endpoint_and_expected_responses()
    {
        using var development = new AuditSqlServerFixture.AuditFactory(fixture.Connection, "Development");
        using var client = development.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var path = document.RootElement.GetProperty("paths").GetProperty("/api/audit/logs");
        Assert.Single(path.EnumerateObject());
        var get = path.GetProperty("get");
        Assert.Equal("GetAuditLogs", get.GetProperty("operationId").GetString());
        foreach (var status in new[] { "200", "400", "401", "403" }) Assert.True(get.GetProperty("responses").TryGetProperty(status, out _));
        Assert.Equal(new[] { "actorId", "entityId", "entityType", "fromUtc", "page", "pageSize", "toUtc" },
            get.GetProperty("parameters").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Order());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class AuditSqlServerFixture : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantAuditTest_" + Guid.NewGuid().ToString("N");
    private AuditFactory? factory;
    public WebApplicationFactory<Program> Host => factory ?? throw new InvalidOperationException("SQL fixture is not configured.");
    public string Connection { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        Connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        factory = new AuditFactory(Connection);
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public AuditDbContext Database() => new(new DbContextOptionsBuilder<AuditDbContext>().UseSqlServer(Connection,
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema)).Options);

    public async Task<(Guid UserId, string Token)> LoginAsync(string role)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";
        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = "audit_" + Guid.NewGuid().ToString("N") };
        user.Email = user.UserName + "@example.invalid";
        await using (var scope = Host.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        using var client = Host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/identity/login", new LoginRequest(user.UserName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (user.Id, (await response.Content.ReadFromJsonAsync<LoginResult>())!.AccessToken);
    }

    public HttpClient Client(string token)
    {
        var client = Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        if (factory is null) return;
        try
        {
            await using var db = Database();
            var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
            if (target != databaseName || !target.StartsWith("RestaurantAuditTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe audit test cleanup target.");
            await db.Database.EnsureDeletedAsync();
        }
        finally { await factory.DisposeAsync(); }
    }

    internal sealed class AuditFactory(string connection, string environment = "Testing") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
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
