using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Shared.SqlServer;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>
/// HIE-3.4 foundation on a real SQL Server: the InitialFulfillment migration, the ticket and incident rules the database
/// enforces, and the Outbox and Inbox tables matching what Restaurant.Shared.SqlServer expects.
/// </summary>
public sealed class FulfillmentSqlServerTests : IAsyncLifetime
{
    private static readonly Guid Staff = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    private readonly string databaseName = "RestaurantFulfillmentTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    private FulfillmentDbContext CreateContext() => new(new DbContextOptionsBuilder<FulfillmentDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "fulfillment")).Options);

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName ||
            !databaseName.StartsWith("RestaurantFulfillmentTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        SqlConnection.ClearAllPools();
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    // ---- domain factories (no database) ----

    [Fact]
    public void The_first_ticket_is_attempt_one_new_and_has_no_original_or_incident()
    {
        var result = PhieuCheBien.CreateInitial(42, Stations.Bar, Now);

        Assert.True(result.Succeeded);
        var ticket = result.Value!;
        Assert.Equal((42L, 1, "BAR", TicketStatus.New), (ticket.ChiTietDonId, ticket.Lan, ticket.TramChot, ticket.TrangThai));
        Assert.Null(ticket.PhieuGocId);
        Assert.Null(ticket.SuCoNguonId);
        Assert.Equal(Now, ticket.TaoLuc);
    }

    [Theory]
    [InlineData(0, "BEP")]
    [InlineData(-1, "BEP")]
    [InlineData(1, null)]
    [InlineData(1, "")]
    [InlineData(1, "bep")]
    [InlineData(1, "KITCHEN")]
    public void A_ticket_needs_a_real_portion_and_a_known_station(long portion, string? station) =>
        Assert.Equal(FulfillmentErrors.InputInvalid, PhieuCheBien.CreateInitial(portion, station, Now).Error);

    [Fact]
    public void An_incident_needs_a_ticket_a_reason_a_reporter_and_a_request_key()
    {
        var key = Guid.NewGuid();
        Assert.Equal(FulfillmentErrors.InputInvalid, SuCoMon.Report(0, "Burnt", Staff, key, Now).Error);
        Assert.Equal(FulfillmentErrors.InputInvalid, SuCoMon.Report(1, null, Staff, key, Now).Error);
        Assert.Equal(FulfillmentErrors.InputInvalid, SuCoMon.Report(1, "   ", Staff, key, Now).Error);
        Assert.Equal(FulfillmentErrors.InputInvalid, SuCoMon.Report(1, new string('x', 1001), Staff, key, Now).Error);
        Assert.Equal(FulfillmentErrors.InputInvalid, SuCoMon.Report(1, "Burnt", Guid.Empty, key, Now).Error);
        Assert.Equal(FulfillmentErrors.InputInvalid, SuCoMon.Report(1, "Burnt", Staff, Guid.Empty, Now).Error);

        var ok = SuCoMon.Report(7, "  Burnt  ", Staff, key, Now).Value!;
        Assert.Equal((7L, "Burnt", IncidentStatus.Pending, key), (ok.PhieuCheBienId, ok.LyDo, ok.TrangThai, ok.KhoaYeuCau));
        Assert.Null(ok.QuyetDinh);
        Assert.True(SuCoMon.Report(7, new string('x', 1000), Staff, key, Now).Succeeded);
    }

    // ---- migration shape ----

    [Fact]
    public void Model_and_migration_define_two_business_tables_and_the_technical_ones_without_cascades()
    {
        using var db = new FulfillmentDbContext(new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseSqlServer(ApiFactory.ConnectionString).Options);
        Assert.Equal(5, db.Model.GetEntityTypes().Count());
        Assert.Equal(2, db.Database.GetMigrations().Count());
        Assert.All(db.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()), fk =>
        {
            Assert.Equal("fulfillment", fk.PrincipalEntityType.GetSchema());
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        });
        var script = db.Database.GenerateCreateScript();
        Assert.DoesNotContain("CASCADE", script);
        Assert.Contains("[fulfillment].[Inbox]", script);
        Assert.Contains("[fulfillment].[Outbox]", script);
    }

    // ---- EF round trip ----

    [SqlServerFact]
    public async Task A_ticket_and_an_incident_round_trip_through_the_database()
    {
        await using (var db = CreateContext())
        {
            var ticket = PhieuCheBien.CreateInitial(500, Stations.Kitchen, Now).Value!;
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            Assert.True(ticket.Id > 0);
            Assert.NotEmpty(ticket.Version);
            var incident = SuCoMon.Report(ticket.Id, "Wrong dish", Staff, Guid.NewGuid(), Now).Value!;
            db.Incidents.Add(incident);
            await db.SaveChangesAsync();
            Assert.True(incident.Id > 0);
        }

        await using var read = CreateContext();
        var saved = await read.Tickets.AsNoTracking().SingleAsync();
        Assert.Equal((500L, 1, "BEP", TicketStatus.New, Now), (saved.ChiTietDonId, saved.Lan, saved.TramChot, saved.TrangThai, saved.TaoLuc));
        var savedIncident = await read.Incidents.AsNoTracking().SingleAsync();
        Assert.Equal((saved.Id, "Wrong dish", IncidentStatus.Pending), (savedIncident.PhieuCheBienId, savedIncident.LyDo, savedIncident.TrangThai));
    }

    // ---- ticket rules ----

    [SqlServerFact]
    public async Task One_portion_has_one_ticket_per_attempt_so_a_replayed_event_cannot_duplicate_it()
    {
        await ExecAsync(TicketSql(10, 1));
        Assert.Equal(2601, await ErrorAsync(TicketSql(10, 1)));
        await ExecAsync(TicketSql(11, 1));
        Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM fulfillment.PhieuCheBien"));
    }

    [SqlServerFact]
    public async Task A_ticket_must_match_its_station_status_and_attempt()
    {
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, station: "'KITCHEN'")));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'DONE'")));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 0)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, -1)));
        var portion = 100;
        foreach (var status in new[] { TicketStatus.New, TicketStatus.Cancelled, TicketStatus.Failed })
            await ExecAsync(TicketSql(portion++, 1, status: $"'{status}'"));
    }

    [SqlServerFact]
    public async Task Attempt_one_has_no_original_or_incident_and_a_remake_has_both()
    {
        await ExecAsync(TicketSql(10, 1));
        var first = await ScalarAsync("SELECT MAX(Id) FROM fulfillment.PhieuCheBien");
        await ExecAsync(IncidentSql(first));
        var incident = await ScalarAsync("SELECT MAX(Id) FROM fulfillment.SuCoMon");

        // Attempt 1 must not point back; a later attempt must point to both.
        Assert.Equal(547, await ErrorAsync(TicketSql(11, 1, original: first.ToString())));
        Assert.Equal(547, await ErrorAsync(TicketSql(11, 1, incident: incident.ToString())));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 2)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 2, original: first.ToString())));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 2, incident: incident.ToString())));
        await ExecAsync(TicketSql(10, 2, original: first.ToString(), incident: incident.ToString()));
    }

    [SqlServerFact]
    public async Task One_incident_allows_one_remake_and_one_ticket_is_remade_once()
    {
        await ExecAsync(TicketSql(10, 1));
        var first = await ScalarAsync("SELECT MAX(Id) FROM fulfillment.PhieuCheBien");
        await ExecAsync(IncidentSql(first));
        await ExecAsync(IncidentSql(first));
        var incidents = await IdsAsync("SELECT Id FROM fulfillment.SuCoMon ORDER BY Id");
        await ExecAsync(TicketSql(10, 2, original: first.ToString(), incident: incidents[0].ToString()));

        // Same incident again, and a different incident on an already remade ticket: both would fork the chain.
        Assert.Equal(2601, await ErrorAsync(TicketSql(10, 3, original: first.ToString(), incident: incidents[0].ToString())));
        Assert.Equal(2601, await ErrorAsync(TicketSql(10, 3, original: first.ToString(), incident: incidents[1].ToString())));
        Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM fulfillment.PhieuCheBien"));
    }

    [SqlServerFact]
    public async Task Progress_columns_must_agree_with_the_ticket_status()
    {
        // A new ticket has not been touched.
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, started: true)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, finished: true)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, delivered: true)));
        // Who and when always come as a pair.
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'DANG_LAM'", startedAtOnly: true)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'DANG_LAM'", startedByOnly: true)));
        // Each later status needs its own evidence.
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'DANG_LAM'")));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'SAN_SANG'", started: true)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'DA_GIAO'", started: true, finished: true)));
        Assert.Equal(547, await ErrorAsync(TicketSql(10, 1, status: "'DA_GIAO'", delivered: true)));

        await ExecAsync(TicketSql(10, 1, status: "'DANG_LAM'", started: true));
        await ExecAsync(TicketSql(11, 1, status: "'SAN_SANG'", started: true, finished: true));
        await ExecAsync(TicketSql(12, 1, status: "'DA_GIAO'", started: true, finished: true, delivered: true));
    }

    // ---- incident rules ----

    [SqlServerFact]
    public async Task An_incident_must_belong_to_a_ticket_and_carry_a_reason_and_a_unique_request_key()
    {
        Assert.Equal(547, await ErrorAsync(IncidentSql(999_999)));
        await ExecAsync(TicketSql(10, 1));
        var ticket = await ScalarAsync("SELECT MAX(Id) FROM fulfillment.PhieuCheBien");
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, reason: "N''")));
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, reason: "N'   '")));
        var key = Guid.NewGuid();
        await ExecAsync(IncidentSql(ticket, key: key));
        Assert.Equal(2601, await ErrorAsync(IncidentSql(ticket, key: key)));
    }

    [SqlServerFact]
    public async Task An_incident_decision_must_agree_with_its_status()
    {
        await ExecAsync(TicketSql(10, 1));
        var ticket = await ScalarAsync("SELECT MAX(Id) FROM fulfillment.PhieuCheBien");

        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, status: "'OPEN'")));
        // Pending: nothing decided yet.
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, decision: "'LAM_LAI'")));
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, approver: true)));
        // Approved: a known decision, who and when.
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, status: "'DA_DUYET'")));
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, status: "'DA_DUYET'", decision: "'LAM_LAI'")));
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, status: "'DA_DUYET'", decision: "'KHAC'", approver: true)));
        // Rejected: who and when, but no decision.
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, status: "'TU_CHOI'")));
        Assert.Equal(547, await ErrorAsync(IncidentSql(ticket, status: "'TU_CHOI'", decision: "'LAM_LAI'", approver: true)));

        await ExecAsync(IncidentSql(ticket));
        foreach (var decision in IncidentDecision.All)
            await ExecAsync(IncidentSql(ticket, status: "'DA_DUYET'", decision: $"'{decision}'", approver: true));
        await ExecAsync(IncidentSql(ticket, status: "'TU_CHOI'", approver: true));
        Assert.Equal(5, await CountAsync("SELECT COUNT(*) FROM fulfillment.SuCoMon"));
    }

    // ---- Outbox and Inbox tables ----

    [SqlServerFact]
    public async Task The_inbox_table_works_with_the_shared_duplicate_guard_and_survives_a_rollback()
    {
        var eventId = Guid.NewGuid();
        await using var connection = await OpenAsync();

        await using (var first = await connection.BeginTransactionAsync())
        {
            Assert.True(await InboxStore.TryRecordAsync(connection, first, "fulfillment", "fulfillment.tickets", eventId, Now));
            await first.RollbackAsync();
        }
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM fulfillment.Inbox"));

        await using (var second = await connection.BeginTransactionAsync())
        {
            Assert.True(await InboxStore.TryRecordAsync(connection, second, "fulfillment", "fulfillment.tickets", eventId, Now));
            await second.CommitAsync();
        }
        await using (var repeat = await connection.BeginTransactionAsync())
        {
            Assert.False(await InboxStore.TryRecordAsync(connection, repeat, "fulfillment", "fulfillment.tickets", eventId, Now));
            // Another consumer of the same event is independent.
            Assert.True(await InboxStore.TryRecordAsync(connection, repeat, "fulfillment", "fulfillment.other", eventId, Now));
            await repeat.CommitAsync();
        }
        Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM fulfillment.Inbox"));
    }

    [SqlServerFact]
    public async Task The_outbox_table_works_with_the_shared_dispatcher_queries()
    {
        var id = Guid.NewGuid();
        await ExecAsync($"""
            INSERT INTO fulfillment.Outbox (Id, AggregateId, AggregateVersion, Type, SchemaVersion, Payload, OccurredAt, NextAttemptAt, Attempts)
            VALUES ('{id}', 'ticket-1', 1, 'fulfillment.ticket_ready', 1, N'payload', '2026-11-20T10:00:00', '2026-11-20T10:00:00', 0);
            """);
        // The same fact (type, aggregate, version) cannot be staged twice.
        Assert.Equal(2601, await ErrorAsync($"""
            INSERT INTO fulfillment.Outbox (Id, AggregateId, AggregateVersion, Type, SchemaVersion, Payload, OccurredAt, NextAttemptAt, Attempts)
            VALUES ('{Guid.NewGuid()}', 'ticket-1', 1, 'fulfillment.ticket_ready', 1, N'payload', '2026-11-20T10:00:00', '2026-11-20T10:00:00', 0);
            """));

        await using var connection = await OpenAsync();
        var due = Now.AddMinutes(1);
        var claimed = await OutboxStore.ClaimDueAsync(connection, "fulfillment", new DateTimeOffset(due, TimeSpan.Zero), 10, TimeSpan.FromMinutes(1));
        var message = Assert.Single(claimed);
        Assert.Equal((id, "fulfillment.ticket_ready", 1), (message.EventId, message.Type, message.Attempts));
        Assert.True(await OutboxStore.MarkProcessedAsync(connection, "fulfillment", id, new DateTimeOffset(due, TimeSpan.Zero)));
        Assert.Empty(await OutboxStore.ClaimDueAsync(connection, "fulfillment", new DateTimeOffset(due.AddHours(1), TimeSpan.Zero), 10, TimeSpan.FromMinutes(1)));
    }

    // ---- helpers ----

    private static string TicketSql(
        long portion, int attempt, string station = "'BEP'", string status = "'MOI'", string original = "NULL", string incident = "NULL",
        bool started = false, bool finished = false, bool delivered = false, bool startedAtOnly = false, bool startedByOnly = false) => $"""
        INSERT INTO fulfillment.PhieuCheBien (ChiTietDonId, Lan, TramChot, PhieuGocId, SuCoNguonId, TrangThai, TaoLuc,
            NguoiBatDauId, BatDauLuc, NguoiHoanThanhId, HoanThanhLuc, NguoiGiaoId, GiaoLuc)
        VALUES ({portion}, {attempt}, {station}, {original}, {incident}, {status}, SYSUTCDATETIME(),
            {(started || startedByOnly ? $"'{Staff}'" : "NULL")}, {(started || startedAtOnly ? "SYSUTCDATETIME()" : "NULL")},
            {(finished ? $"'{Staff}'" : "NULL")}, {(finished ? "SYSUTCDATETIME()" : "NULL")},
            {(delivered ? $"'{Staff}'" : "NULL")}, {(delivered ? "SYSUTCDATETIME()" : "NULL")});
        """;

    private static string IncidentSql(
        long ticket, string reason = "N'Burnt'", string status = "'CHO_DUYET'", string decision = "NULL",
        bool approver = false, Guid? key = null) => $"""
        INSERT INTO fulfillment.SuCoMon (PhieuCheBienId, LyDo, BaoLuc, NguoiBaoId, TrangThai, QuyetDinh,
            NguoiDuyetId, DuyetLuc, KhoaYeuCau)
        VALUES ({ticket}, {reason}, SYSUTCDATETIME(), '{Staff}', {status}, {decision},
            {(approver ? $"'{Staff}'" : "NULL")}, {(approver ? "SYSUTCDATETIME()" : "NULL")}, '{key ?? Guid.NewGuid()}');
        """;

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task ExecAsync(string sql)
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<int> ErrorAsync(string sql)
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => ExecAsync(sql));
        return error.Number;
    }

    private async Task<int> CountAsync(string sql)
    {
        await using var db = CreateContext();
        return await db.Database.SqlQueryRaw<int>(sql.Replace("COUNT(*)", "COUNT(*) AS [Value]", StringComparison.Ordinal)).SingleAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var db = CreateContext();
        return await db.Database.SqlQueryRaw<long>(sql.Replace("MAX(Id)", "MAX(Id) AS [Value]", StringComparison.Ordinal)).SingleAsync();
    }

    private async Task<long[]> IdsAsync(string sql)
    {
        await using var db = CreateContext();
        return await db.Database.SqlQueryRaw<long>(sql.Replace("SELECT Id", "SELECT Id AS [Value]", StringComparison.Ordinal)).ToArrayAsync();
    }
}
