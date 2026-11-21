using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Contracts;
using Restaurant.Modules.Seating.Infrastructure;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>ISessionLookup (Seating side of the HIE-3.1 contract) on a real SQL Server.</summary>
public sealed class SeatingSessionLookupSqlServerTests : IAsyncLifetime
{
    private static readonly Guid Staff = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string databaseName = "RestaurantSeatingLookupTest_" + Guid.NewGuid().ToString("N");
    private readonly FakeDays days = new();
    private string? connectionString;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        if (!databaseName.StartsWith("RestaurantSeatingLookupTest_", StringComparison.Ordinal) ||
            new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName)
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        SqlConnection.ClearAllPools();
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    private SeatingDbContext NewContext() => new(new DbContextOptionsBuilder<SeatingDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "seating")).Options);

    private async Task<long> CreateTableAsync(string code, string name)
    {
        await using var db = NewContext();
        var catalog = new SeatingCatalog(db);
        var area = await catalog.CreateAreaAsync(new CreateAreaRequest("A" + code, "Area " + code), default);
        Assert.Null(area.Error);
        var table = await catalog.CreateTableAsync(new CreateTableRequest(area.Value!.Id, code, name), default);
        Assert.Null(table.Error);
        return table.Value!.Id;
    }

    private async Task<SessionDto> OpenAsync(long tableId)
    {
        await using var db = NewContext();
        var sessions = new SeatingSessions(db, days, new OpenGuard(), TimeProvider.System);
        var result = await sessions.OpenAsync(tableId, Staff, Guid.NewGuid(), default);
        Assert.Null(result.Error);
        return result.Value!;
    }

    private async Task<SessionCheckResult> CheckAsync(long sessionId)
    {
        await using var db = NewContext();
        return await new SeatingSessionLookup(db).CheckOpenAsync(sessionId, default);
    }

    [SqlServerFact]
    public async Task An_open_session_reports_its_day_table_and_opening_time()
    {
        var table = await CreateTableAsync("T1", "Table 1");
        var session = await OpenAsync(table);

        var result = await CheckAsync(session.Id);

        Assert.True(result.IsOpen);
        Assert.Null(result.ErrorCode);
        var info = result.Session!;
        Assert.Equal(session.Id, info.SessionId);
        Assert.Equal(days.Day, info.BusinessDayId);
        Assert.Equal(table, info.TableId);
        Assert.Equal("Table 1", info.TableLabel);
        Assert.Equal(TimeSpan.Zero, info.OpenedAt.Offset);
        Assert.InRange(info.OpenedAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [SqlServerFact]
    public async Task An_unknown_or_invalid_session_id_is_not_found()
    {
        await CreateTableAsync("T1", "Table 1");
        foreach (var id in new long[] { 999_999, 0, -1, long.MinValue })
        {
            var result = await CheckAsync(id);
            Assert.False(result.IsOpen);
            Assert.Null(result.Session);
            Assert.Equal(SeatingErrorCodes.SessionNotFound, result.ErrorCode);
        }
    }

    [SqlServerFact]
    public async Task A_closed_session_is_reported_closed_not_missing()
    {
        var table = await CreateTableAsync("T1", "Table 1");
        var session = await OpenAsync(table);
        await using (var db = NewContext())
        {
            var closed = await new SeatingSessions(db, days, new OpenGuard(), TimeProvider.System)
                .CloseAsync(session.Id, Staff, Guid.NewGuid(), default);
            Assert.Null(closed.Error);
        }

        var result = await CheckAsync(session.Id);

        Assert.False(result.IsOpen);
        Assert.Null(result.Session);
        Assert.Equal(SeatingErrorCodes.SessionClosed, result.ErrorCode);
    }

    [SqlServerFact]
    public async Task After_a_transfer_the_lookup_reports_the_new_table()
    {
        var oldTable = await CreateTableAsync("T1", "Table 1");
        var newTable = await CreateTableAsync("T2", "Table 2");
        var session = await OpenAsync(oldTable);
        await using (var db = NewContext())
        {
            var moved = await new SeatingSessions(db, days, new OpenGuard(), TimeProvider.System)
                .TransferAsync(session.Id, new TransferRequest(newTable, "Customer request"), Staff, Guid.NewGuid(), default);
            Assert.Null(moved.Error);
        }

        var info = (await CheckAsync(session.Id)).Session!;

        Assert.Equal(newTable, info.TableId);
        Assert.Equal("Table 2", info.TableLabel);
    }

    [SqlServerFact]
    public async Task A_new_session_on_a_freed_table_does_not_resurrect_the_old_one()
    {
        var table = await CreateTableAsync("T1", "Table 1");
        var first = await OpenAsync(table);
        await using (var db = NewContext())
            Assert.Null((await new SeatingSessions(db, days, new OpenGuard(), TimeProvider.System)
                .CloseAsync(first.Id, Staff, Guid.NewGuid(), default)).Error);
        var second = await OpenAsync(table);

        Assert.Equal(SeatingErrorCodes.SessionClosed, (await CheckAsync(first.Id)).ErrorCode);
        Assert.Equal(second.Id, (await CheckAsync(second.Id)).Session!.SessionId);
    }

    [Fact]
    public async Task Invalid_ids_are_answered_without_touching_the_database()
    {
        // No connection string: a query would throw. Proves the guard clause runs before any I/O.
        await using var db = new SeatingDbContext(new DbContextOptionsBuilder<SeatingDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=unused;Connect Timeout=1").Options);
        var result = await new SeatingSessionLookup(db).CheckOpenAsync(0, default);
        Assert.Equal(SeatingErrorCodes.SessionNotFound, result.ErrorCode);
    }

    private sealed class FakeDays : IBusinessDayDirectory
    {
        public Guid Day { get; } = Guid.NewGuid();
        public Task<Guid?> CurrentOpenDayAsync(CancellationToken ct) => Task.FromResult<Guid?>(Day);
        public Task<bool> IsOpenAsync(Guid dayId, CancellationToken ct) => Task.FromResult(dayId == Day);
        public Task<bool> RequireOpenAsync(System.Data.Common.DbTransaction transaction, Guid dayId, CancellationToken ct) =>
            Task.FromResult(dayId == Day);
    }

    private sealed class OpenGuard : ISessionClosureGuard
    {
        public Task<bool> HasOpenOrdersAsync(long sessionId, CancellationToken ct) => Task.FromResult(false);
    }
}
