using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Infrastructure;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>HIE-2.1 on a real SQL Server: concurrency (test 3), idempotency, day gate and grant revocation.</summary>
public sealed class SeatingServiceSqlServerTests : IAsyncLifetime
{
    private static readonly Guid Staff = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStaff = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly string databaseName = "RestaurantSeatingApiTest_" + Guid.NewGuid().ToString("N");
    private readonly FakeDays days = new();
    private readonly FakeGuard guard = new();
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
        if (!databaseName.StartsWith("RestaurantSeatingApiTest_", StringComparison.Ordinal) ||
            new SqlConnectionStringBuilder(connectionString).InitialCatalog != databaseName)
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        SqlTestDatabase.ClearPool(connectionString);
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    private SeatingDbContext NewContext() => new(new DbContextOptionsBuilder<SeatingDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "seating")).Options);

    private (SeatingCatalog Catalog, SeatingSessions Sessions, SeatingDbContext Db) NewServices()
    {
        var db = NewContext();
        return (new SeatingCatalog(db, TestBusinessAudit.Instance), new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance), db);
    }

    private async Task<long> CreateAreaAsync(string code = "A")
    {
        var (catalog, _, db) = NewServices();
        await using var _ = db;
        var result = await catalog.CreateAreaAsync(new CreateAreaRequest(code, "Area " + code), default);
        Assert.Null(result.Error);
        return result.Value!.Id;
    }

    private async Task<long> CreateTableAsync(long areaId, string code)
    {
        var (catalog, _, db) = NewServices();
        await using var _ = db;
        var result = await catalog.CreateTableAsync(new CreateTableRequest(areaId, code, "Table " + code), default);
        Assert.Null(result.Error);
        return result.Value!.Id;
    }

    private async Task<SessionDto> OpenOkAsync(long tableId)
    {
        var (_, sessions, db) = NewServices();
        await using var _ = db;
        var result = await sessions.OpenAsync(tableId, Staff, Guid.NewGuid(), default);
        Assert.Null(result.Error);
        return result.Value!;
    }

    private async Task<int> OpenSessionCountAsync(long tableId)
    {
        await using var db = NewContext();
        return await db.Sessions.CountAsync(x => x.BanHienTaiId == tableId && x.DongLuc == null);
    }

    [SqlServerFact]
    public async Task Concurrent_opens_of_one_table_yield_exactly_one_session()
    {
        var table = await CreateTableAsync(await CreateAreaAsync(), "T1");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            var (_, sessions, db) = NewServices();
            await using var __ = db;
            return await sessions.OpenAsync(table, Staff, Guid.NewGuid(), default);
        }));
        Assert.Single(results, r => r.Error is null);
        Assert.All(results.Where(r => r.Error is not null), r => Assert.Equal(SeatingErrors.TableOccupied, r.Error));
        Assert.Equal(1, await OpenSessionCountAsync(table));
    }

    [SqlServerFact]
    public async Task Concurrent_transfers_into_one_free_table_move_only_one_session()
    {
        var area = await CreateAreaAsync();
        var (t1, t2, t3) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T2"), await CreateTableAsync(area, "T3"));
        var s1 = await OpenOkAsync(t1);
        var s2 = await OpenOkAsync(t2);
        var results = await Task.WhenAll(new[] { s1, s2 }.Select(async s =>
        {
            var (_, sessions, db) = NewServices();
            await using var _ = db;
            return await sessions.TransferAsync(s.Id, new TransferRequest(t3, "Customer request"), Staff, Guid.NewGuid(), default);
        }));
        Assert.Single(results, r => r.Error is null);
        Assert.Single(results, r => r.Error is not null);
        Assert.Equal(1, await OpenSessionCountAsync(t3));
        await using var check = NewContext();
        Assert.Equal(1, await check.Transfers.CountAsync());
        Assert.Equal(2, await check.Sessions.CountAsync(x => x.DongLuc == null));
    }

    [SqlServerFact]
    public async Task Open_and_transfer_racing_for_one_table_never_occupy_it_twice()
    {
        var area = await CreateAreaAsync();
        var (t1, t3) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T3"));
        var moving = await OpenOkAsync(t1);
        var tasks = new[]
        {
            Task.Run(async () =>
            {
                var (_, sessions, db) = NewServices();
                await using var _ = db;
                return await sessions.OpenAsync(t3, Staff, Guid.NewGuid(), default);
            }),
            Task.Run(async () =>
            {
                var (_, sessions, db) = NewServices();
                await using var _ = db;
                return await sessions.TransferAsync(moving.Id, new TransferRequest(t3, "Moved"), Staff, Guid.NewGuid(), default);
            })
        };
        var results = await Task.WhenAll(tasks);
        Assert.Single(results, r => r.Error is null);
        Assert.Equal(1, await OpenSessionCountAsync(t3));
    }

    [SqlServerFact]
    public async Task Transfer_records_history_frees_the_old_table_and_keeps_guest_access()
    {
        var area = await CreateAreaAsync();
        var (t1, t2) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T2"));
        var session = await OpenOkAsync(t1);
        await InsertGrantAsync(session.Id, Guid.NewGuid(), "token-1");

        var (_, sessions, db) = NewServices();
        await using var _ = db;
        var moved = await sessions.TransferAsync(session.Id, new TransferRequest(t2, "  Window seat  "), OtherStaff, Guid.NewGuid(), default);

        Assert.Null(moved.Error);
        Assert.Equal(t2, moved.Value!.TableId);
        Assert.Equal(session.Id, moved.Value.Id);
        await using var check = NewContext();
        var history = await check.Transfers.SingleAsync();
        Assert.Equal((session.Id, t1, t2, OtherStaff, "Window seat"),
            (history.PhienSuDungId, history.BanCuId, history.BanMoiId, history.NguoiChuyenId, history.LyDo));
        Assert.Equal(0, await OpenSessionCountAsync(t1));
        Assert.Equal(1, await OpenSessionCountAsync(t2));
        var grant = await check.Grants.SingleAsync();
        Assert.Null(grant.ThuHoiLuc);
        Assert.Equal(session.Id, grant.PhienSuDungId);
        await using var reopen = NewContext();
        var again = await new SeatingSessions(reopen, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(t1, Staff, Guid.NewGuid(), default);
        Assert.Null(again.Error);
    }

    [SqlServerFact]
    public async Task Transfer_rejects_occupied_same_inactive_unknown_and_closed_cases()
    {
        var area = await CreateAreaAsync();
        var (t1, t2, t3) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T2"), await CreateTableAsync(area, "T3"));
        var s1 = await OpenOkAsync(t1);
        await OpenOkAsync(t2);
        var (catalog, sessions, db) = NewServices();
        await using var _ = db;
        var table3 = (await catalog.ListTablesAsync(default)).Single(x => x.Id == t3);
        Assert.Null((await catalog.ArchiveTableAsync(t3, new ArchiveRequest(table3.Version), default)).Error);

        async Task<string?> Try(long target, string reason = "Move")
        {
            await using var inner = NewContext();
            return (await new SeatingSessions(inner, days, guard, TimeProvider.System, TestBusinessAudit.Instance)
                .TransferAsync(s1.Id, new TransferRequest(target, reason), Staff, Guid.NewGuid(), default)).Error;
        }

        Assert.Equal(SeatingErrors.TableOccupied, await Try(t2));
        Assert.Equal(SeatingErrors.TransferSameTable, await Try(t1));
        Assert.Equal(SeatingErrors.TableInactive, await Try(t3));
        Assert.Equal(SeatingErrors.TableNotFound, await Try(9999));
        Assert.Equal(SeatingErrors.InputInvalid, await Try(t2, "   "));
        Assert.Equal(SeatingErrors.SessionNotFound, (await sessions.TransferAsync(9999, new TransferRequest(t2, "x"), Staff, Guid.NewGuid(), default)).Error);

        await using var closing = NewContext();
        Assert.Null((await new SeatingSessions(closing, days, guard, TimeProvider.System, TestBusinessAudit.Instance).CloseAsync(s1.Id, Staff, Guid.NewGuid(), default)).Error);
        Assert.Equal(SeatingErrors.SessionClosed, await Try(t2));
        await using var check = NewContext();
        Assert.Empty(check.Transfers);
    }

    [SqlServerFact]
    public async Task Close_revokes_every_unrevoked_guest_grant_and_frees_the_table()
    {
        var table = await CreateTableAsync(await CreateAreaAsync(), "T1");
        var session = await OpenOkAsync(table);
        var earlier = DateTime.UtcNow.AddMinutes(-30);
        await InsertGrantAsync(session.Id, Guid.NewGuid(), "a");
        await InsertGrantAsync(session.Id, Guid.NewGuid(), "b");
        var alreadyRevoked = Guid.NewGuid();
        await InsertGrantAsync(session.Id, alreadyRevoked, "c-already-revoked", revokedAt: earlier);
        var otherSession = await OpenOkAsync(await CreateTableAsync(await CreateAreaAsync("B"), "T2"));
        await InsertGrantAsync(otherSession.Id, Guid.NewGuid(), "other");

        var (_, sessions, db) = NewServices();
        await using var _ = db;
        var closed = await sessions.CloseAsync(session.Id, OtherStaff, Guid.NewGuid(), default);

        Assert.Null(closed.Error);
        Assert.NotNull(closed.Value!.ClosedAt);
        Assert.Equal(OtherStaff, closed.Value.ClosedBy);
        await using var check = NewContext();
        var grants = await check.Grants.AsNoTracking().Where(x => x.PhienSuDungId == session.Id).ToListAsync();
        Assert.Equal(3, grants.Count);
        Assert.All(grants, g => Assert.NotNull(g.ThuHoiLuc));
        // A grant revoked earlier keeps its original revocation time.
        Assert.Equal(earlier, grants.Single(g => g.Id == alreadyRevoked).ThuHoiLuc!.Value, TimeSpan.FromSeconds(1));
        Assert.All(grants.Where(g => g.Id != alreadyRevoked), g => Assert.True(g.ThuHoiLuc!.Value > earlier.AddMinutes(20)));
        Assert.Null((await check.Grants.SingleAsync(x => x.PhienSuDungId == otherSession.Id)).ThuHoiLuc);
        Assert.Equal(0, await OpenSessionCountAsync(table));
    }

    [SqlServerFact]
    public async Task Close_is_refused_while_orders_are_open_and_when_already_closed()
    {
        var session = await OpenOkAsync(await CreateTableAsync(await CreateAreaAsync(), "T1"));
        guard.Blocked = true;
        var (_, sessions, db) = NewServices();
        await using var _ = db;
        Assert.Equal(SeatingErrors.SessionHasOpenOrders, (await sessions.CloseAsync(session.Id, Staff, Guid.NewGuid(), default)).Error);

        guard.Blocked = false;
        await using var second = NewContext();
        Assert.Null((await new SeatingSessions(second, days, guard, TimeProvider.System, TestBusinessAudit.Instance).CloseAsync(session.Id, Staff, Guid.NewGuid(), default)).Error);
        await using var third = NewContext();
        Assert.Equal(SeatingErrors.SessionClosed, (await new SeatingSessions(third, days, guard, TimeProvider.System, TestBusinessAudit.Instance).CloseAsync(session.Id, Staff, Guid.NewGuid(), default)).Error);
        await using var fourth = NewContext();
        Assert.Equal(SeatingErrors.SessionNotFound, (await new SeatingSessions(fourth, days, guard, TimeProvider.System, TestBusinessAudit.Instance).CloseAsync(9999, Staff, Guid.NewGuid(), default)).Error);
    }

    [SqlServerFact]
    public async Task Same_key_replays_without_a_second_session_and_other_payloads_conflict()
    {
        var area = await CreateAreaAsync();
        var (t1, t2) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T2"));
        var key = Guid.NewGuid();

        async Task<SeatingResult<SessionDto>> Open(long table, Guid actor)
        {
            await using var db = NewContext();
            return await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(table, actor, key, default);
        }

        var first = await Open(t1, Staff);
        var replay = await Open(t1, Staff);
        Assert.Null(first.Error);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);
        Assert.Equal(SeatingErrors.IdempotencyConflict, (await Open(t2, Staff)).Error);
        // The same key from another actor is a different request scope.
        Assert.Null((await Open(t2, OtherStaff)).Error);
        await using var check = NewContext();
        Assert.Equal(2, await check.Sessions.CountAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_requests_with_the_same_key_create_one_session()
    {
        var table = await CreateTableAsync(await CreateAreaAsync(), "T1");
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = NewContext();
            return await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(table, Staff, key, default);
        }));
        Assert.All(results, r => Assert.True(r.Error is null || r.Error == SeatingErrors.TableOccupied));
        Assert.Contains(results, r => r.Error is null);
        Assert.Equal(1, await OpenSessionCountAsync(table));
        var ids = results.Where(r => r.Error is null).Select(r => r.Value!.Id).Distinct().ToList();
        Assert.Single(ids);
    }

    [SqlServerFact]
    public async Task A_failed_command_is_not_recorded_so_the_same_key_can_retry()
    {
        var area = await CreateAreaAsync();
        var table = await CreateTableAsync(area, "T1");
        var occupant = await OpenOkAsync(table);
        var key = Guid.NewGuid();
        await using (var db = NewContext())
            Assert.Equal(SeatingErrors.TableOccupied, (await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(table, Staff, key, default)).Error);
        await using (var db = NewContext())
            Assert.Null((await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).CloseAsync(occupant.Id, Staff, Guid.NewGuid(), default)).Error);
        await using (var db = NewContext())
            Assert.Null((await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(table, Staff, key, default)).Error);
    }

    [SqlServerFact]
    public async Task Writes_are_refused_when_no_day_is_open_or_the_day_is_closing()
    {
        var area = await CreateAreaAsync();
        var (t1, t2) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T2"));
        var session = await OpenOkAsync(t1);

        days.Open = false;
        await using (var db = NewContext())
            Assert.Equal(SeatingErrors.DayNotOpen, (await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(t2, Staff, Guid.NewGuid(), default)).Error);
        await using (var db = NewContext())
            Assert.Equal(SeatingErrors.DayNotOpen, (await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).TransferAsync(session.Id, new TransferRequest(t2, "x"), Staff, Guid.NewGuid(), default)).Error);
        await using (var db = NewContext())
            Assert.Equal(SeatingErrors.DayNotOpen, (await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).CloseAsync(session.Id, Staff, Guid.NewGuid(), default)).Error);
        Assert.Equal(1, await OpenSessionCountAsync(t1));
        Assert.Equal(0, await OpenSessionCountAsync(t2));
    }

    [SqlServerFact]
    public async Task A_writer_waits_for_the_closing_day_then_is_refused()
    {
        var table = await CreateTableAsync(await CreateAreaAsync(), "T1");
        await using var closer = new SqlConnection(connectionString);
        await closer.OpenAsync();
        await using var tx = (SqlTransaction)await closer.BeginTransactionAsync();
        await using (var command = closer.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "EXEC sys.sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000";
            command.Parameters.AddWithValue("@r", "restaurant:salesday:" + days.Day.ToString("N"));
            await command.ExecuteNonQueryAsync();
        }

        var writer = Task.Run(async () =>
        {
            await using var db = NewContext();
            return await new SeatingSessions(db, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(table, Staff, Guid.NewGuid(), default);
        });
        await Task.Delay(1500);
        Assert.False(writer.IsCompleted, "The writer must wait while the day is locked exclusively.");

        days.Open = false; // the closer finished freezing the day
        await tx.CommitAsync();
        var result = await writer.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(SeatingErrors.DayNotOpen, result.Error);
        Assert.Equal(0, await OpenSessionCountAsync(table));
    }

    [SqlServerFact]
    public async Task Opening_needs_an_active_table_in_an_active_area()
    {
        var area = await CreateAreaAsync();
        var table = await CreateTableAsync(area, "T1");
        var (catalog, sessions, db) = NewServices();
        await using var _ = db;
        Assert.Equal(SeatingErrors.TableNotFound, (await sessions.OpenAsync(9999, Staff, Guid.NewGuid(), default)).Error);
        var listed = (await catalog.ListTablesAsync(default)).Single();
        Assert.Null((await catalog.ArchiveTableAsync(table, new ArchiveRequest(listed.Version), default)).Error);
        await using var next = NewContext();
        Assert.Equal(SeatingErrors.TableInactive, (await new SeatingSessions(next, days, guard, TimeProvider.System, TestBusinessAudit.Instance).OpenAsync(table, Staff, Guid.NewGuid(), default)).Error);
    }

    [SqlServerFact]
    public async Task Catalog_enforces_unique_codes_active_parents_versions_and_archive_rules()
    {
        var (catalog, _, db) = NewServices();
        await using var _ = db;
        var area = (await catalog.CreateAreaAsync(new CreateAreaRequest(" A1 ", " Garden "), default)).Value!;
        Assert.Equal(("A1", "Garden", true), (area.Code, area.Name, area.Active));
        Assert.Equal(SeatingErrors.AreaCodeConflict, (await catalog.CreateAreaAsync(new CreateAreaRequest("a1", "Dup"), default)).Error);
        Assert.Equal(SeatingErrors.InputInvalid, (await catalog.CreateAreaAsync(new CreateAreaRequest("bad code!", "x"), default)).Error);
        Assert.Equal(SeatingErrors.InputInvalid, (await catalog.CreateAreaAsync(new CreateAreaRequest("A2", " "), default)).Error);

        var table = (await catalog.CreateTableAsync(new CreateTableRequest(area.Id, "T1", "Table 1"), default)).Value!;
        Assert.True(table.PublicQrCode.Length >= 22);
        var second = (await catalog.CreateTableAsync(new CreateTableRequest(area.Id, "T2", "Table 2"), default)).Value!;
        Assert.NotEqual(table.PublicQrCode, second.PublicQrCode);
        Assert.Equal(SeatingErrors.TableCodeConflict, (await catalog.CreateTableAsync(new CreateTableRequest(area.Id, "t1", "Dup"), default)).Error);
        Assert.Equal(SeatingErrors.AreaNotFound, (await catalog.CreateTableAsync(new CreateTableRequest(9999, "T9", "x"), default)).Error);

        // A stale version is refused; the fresh one is accepted and changes the version.
        await using var stale = NewContext();
        var staleCatalog = new SeatingCatalog(stale, TestBusinessAudit.Instance);
        var renamed = (await catalog.UpdateAreaAsync(area.Id, new UpdateAreaRequest("Terrace", area.Version), default)).Value!;
        Assert.NotEqual(area.Version, renamed.Version);
        Assert.Equal(SeatingErrors.ConcurrencyConflict, (await staleCatalog.UpdateAreaAsync(area.Id, new UpdateAreaRequest("Late", area.Version), default)).Error);
        Assert.Equal(SeatingErrors.InputInvalid, (await catalog.UpdateAreaAsync(area.Id, new UpdateAreaRequest("x", "not-base64"), default)).Error);

        // An area with active tables cannot be archived; once they are archived it can.
        Assert.Equal(SeatingErrors.AreaHasActiveTables, (await catalog.ArchiveAreaAsync(area.Id, new ArchiveRequest(renamed.Version), default)).Error);
        await using var tableDb = NewContext();
        var tableCatalog = new SeatingCatalog(tableDb, TestBusinessAudit.Instance);
        foreach (var t in await tableCatalog.ListTablesAsync(default))
            Assert.Null((await tableCatalog.ArchiveTableAsync(t.Id, new ArchiveRequest(t.Version), default)).Error);
        await using var areaDb = NewContext();
        var archived = await new SeatingCatalog(areaDb, TestBusinessAudit.Instance).ArchiveAreaAsync(area.Id, new ArchiveRequest(renamed.Version), default);
        Assert.False(archived.Value!.Active);
        await using var late = NewContext();
        Assert.Equal(SeatingErrors.AreaInactive, (await new SeatingCatalog(late, TestBusinessAudit.Instance).CreateTableAsync(new CreateTableRequest(area.Id, "T3", "x"), default)).Error);
    }

    [SqlServerFact]
    public async Task A_table_with_an_open_session_cannot_be_archived_and_the_board_shows_it_in_use()
    {
        var area = await CreateAreaAsync();
        var (busy, free) = (await CreateTableAsync(area, "T1"), await CreateTableAsync(area, "T2"));
        var session = await OpenOkAsync(busy);
        var (catalog, _, db) = NewServices();
        await using var _ = db;
        var version = (await catalog.ListTablesAsync(default)).Single(x => x.Id == busy).Version;
        Assert.Equal(SeatingErrors.TableInUse, (await catalog.ArchiveTableAsync(busy, new ArchiveRequest(version), default)).Error);

        var board = await catalog.BoardAsync(default);
        var tables = Assert.Single(board).Tables;
        Assert.Equal(BoardStatuses.InUse, tables.Single(x => x.Id == busy).Status);
        Assert.Equal(session.Id, tables.Single(x => x.Id == busy).Session!.Id);
        Assert.Equal(BoardStatuses.Free, tables.Single(x => x.Id == free).Status);
        Assert.Null(tables.Single(x => x.Id == free).Session);
    }

    [SqlServerFact]
    public async Task Moving_a_table_needs_an_active_target_area()
    {
        var (catalog, _, db) = NewServices();
        await using var _ = db;
        var areaA = (await catalog.CreateAreaAsync(new CreateAreaRequest("A", "A"), default)).Value!;
        var areaB = (await catalog.CreateAreaAsync(new CreateAreaRequest("B", "B"), default)).Value!;
        var table = (await catalog.CreateTableAsync(new CreateTableRequest(areaA.Id, "T1", "T1"), default)).Value!;
        await using var archiveDb = NewContext();
        Assert.Null((await new SeatingCatalog(archiveDb, TestBusinessAudit.Instance).ArchiveAreaAsync(areaB.Id, new ArchiveRequest(areaB.Version), default)).Error);
        await using var moveDb = NewContext();
        Assert.Equal(SeatingErrors.AreaInactive,
            (await new SeatingCatalog(moveDb, TestBusinessAudit.Instance).UpdateTableAsync(table.Id, new UpdateTableRequest(areaB.Id, "T1", table.Version), default)).Error);
        await using var okDb = NewContext();
        var renamed = await new SeatingCatalog(okDb, TestBusinessAudit.Instance).UpdateTableAsync(table.Id, new UpdateTableRequest(areaA.Id, "Renamed", table.Version), default);
        Assert.Equal("Renamed", renamed.Value!.Name);
        Assert.Equal(table.PublicQrCode, renamed.Value.PublicQrCode);
    }

    private async Task InsertGrantAsync(long sessionId, Guid id, string token, DateTime? revokedAt = null)
    {
        await using var db = NewContext();
        var created = (revokedAt ?? DateTime.UtcNow).AddMinutes(-5);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO seating.QuyenTruyCapKhach (Id, PhienSuDungId, TokenHash, TaoLuc, HetHanLuc, ThuHoiLuc)
            VALUES ({id}, {sessionId}, CONVERT(binary(32), HASHBYTES('SHA2_256', {token})), {created},
                    {DateTime.UtcNow.AddHours(4)}, {revokedAt});
            """);
    }

    private sealed class FakeDays : IBusinessDayDirectory
    {
        public Guid Day { get; } = Guid.NewGuid();
        public volatile bool Open = true;
        public Task<Guid?> CurrentOpenDayAsync(CancellationToken ct) => Task.FromResult<Guid?>(Open ? Day : null);
        public async Task<bool> RequireOpenAsync(System.Data.Common.DbTransaction transaction, Guid dayId, CancellationToken ct)
        {
            await Restaurant.Shared.SqlServer.BusinessDayLock.AcquireAsync(transaction.Connection!, transaction, dayId,
                Restaurant.Shared.SqlServer.BusinessDayLockMode.Shared, cancellationToken: ct);
            return Open && dayId == Day;
        }
    }

    private sealed class FakeGuard : ISessionClosureGuard
    {
        public volatile bool Blocked;
        public Task<bool> HasOpenOrdersAsync(long sessionId, CancellationToken ct) => Task.FromResult(Blocked);
    }
}
