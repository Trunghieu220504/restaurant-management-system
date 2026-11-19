using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Infrastructure;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;
using Restaurant.Modules.Catalog.Domain;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PhuongWeekOneSqlServerTests : IAsyncLifetime
{
    private readonly string database = "RestaurantPhuongTest_" + Guid.NewGuid().ToString("N");
    private string connectionString = "";
    private SalesDayDbContext Day() => new(new DbContextOptionsBuilder<SalesDayDbContext>().UseSqlServer(connectionString).Options);
    private CatalogDbContext Menu() => new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(connectionString).Options);
    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = database }.ConnectionString;
        await using var day = Day();
        await day.Database.MigrateAsync();
        await using var menu = Menu();
        await menu.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        if (connectionString.Length == 0) return;
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != database || !database.StartsWith("RestaurantPhuongTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe cleanup target");
        await using var db = Day();
        await db.Database.EnsureDeletedAsync();
    }
    [SqlServerFact]
    public async Task Opening_is_serialized_and_replay_does_not_create_another_day()
    {
        var actor = Guid.NewGuid(); var key = Guid.NewGuid();
        var request = new OpenDayRequest(new DateOnly(2026, 11, 9), 100000);
        await using var a = Day(); await using var b = Day();
        var results = await Task.WhenAll(new BusinessDays(a).OpenAsync(request, actor, key, default),
            new BusinessDays(b).OpenAsync(request with { BusinessDate = request.BusinessDate.AddDays(1) }, actor, Guid.NewGuid(), default));
        Assert.Single(results, x => x.Error is null);
        Assert.Single(results, x => x.Error == "salesday.previous_not_closed");
        var winner = results.Single(x => x.Error is null);
        await using var read = Day();
        Assert.Equal(1, await read.Days.CountAsync());
        if (winner.Day!.BusinessDate == request.BusinessDate)
        {
            var service = new BusinessDays(read);
            Assert.True((await service.OpenAsync(request, actor, key, default)).Replayed);
            Assert.Equal("salesday.idempotency.conflict", (await service.OpenAsync(request with { OpeningCash = 1 }, actor, key, default)).Error);
        }
    }
    [SqlServerFact]
    public async Task Database_rejects_a_second_active_day_even_without_the_service()
    {
        await using var db = Day();
        db.Days.Add(new() { Id = Guid.NewGuid(), BusinessDate = new(2026, 11, 9), OpenedBy = Guid.NewGuid(), RequestKey = Guid.NewGuid() });
        await db.SaveChangesAsync();
        db.Days.Add(new() { Id = Guid.NewGuid(), BusinessDate = new(2026, 11, 10), OpenedBy = Guid.NewGuid(), RequestKey = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [SqlServerFact]
    public async Task Catalog_constraints_reject_negative_price_and_invalid_option_range()
    {
        await using var db = Menu();
        var group = new NhomMon { Id = Guid.NewGuid(), Ten = "Food" };
        var dish = new Mon { Id = Guid.NewGuid(), NhomMonId = group.Id, Ma = "A", Ten = "Dish" };
        db.AddRange(group, dish); await db.SaveChangesAsync();
        db.Add(new BienThe { Id = Guid.NewGuid(), MonId = dish.Id, Ma = "S", Ten = "Small", Gia = -1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var options = new NhomTuyChon { Id = Guid.NewGuid(), Ten = "Options" };
        db.Add(options); await db.SaveChangesAsync();
        db.Add(new MonNhomTuyChon { MonId = dish.Id, NhomTuyChonId = options.Id, ChonToiThieu = 2, ChonToiDa = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    private static async Task<int> Lock(SqlConnection connection, SqlTransaction transaction, string mode, int timeout)
    {
        await using var command = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='C03:test', @LockMode=@mode, @LockOwner='Transaction', @LockTimeout=@timeout; SELECT @r;", connection, transaction);
        command.Parameters.AddWithValue("@mode", mode); command.Parameters.AddWithValue("@timeout", timeout);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    [SqlServerFact]
    public async Task C03_shared_locks_coexist_and_exclusive_waits_until_transactions_end()
    {
        await using var a = new SqlConnection(connectionString); await a.OpenAsync();
        await using var b = new SqlConnection(connectionString); await b.OpenAsync();
        await using var ta = (SqlTransaction)await a.BeginTransactionAsync();
        await using var tb = (SqlTransaction)await b.BeginTransactionAsync();
        Assert.True(await Lock(a, ta, "Shared", 0) >= 0);
        Assert.True(await Lock(b, tb, "Shared", 0) >= 0);
        Assert.Equal(-1, await Lock(b, tb, "Exclusive", 0));
        await ta.RollbackAsync();
        Assert.True(await Lock(b, tb, "Exclusive", 1000) >= 0);
        await using var tc = (SqlTransaction)await a.BeginTransactionAsync();
        Assert.Equal(-1, await Lock(a, tc, "Exclusive", 0));
        await tb.RollbackAsync();
        Assert.True(await Lock(a, tc, "Exclusive", 1000) >= 0);
    }
    [SqlServerFact]
    public async Task C03_two_contexts_share_one_transaction_and_rollback_both_modules()
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var day = new SalesDayDbContext(new DbContextOptionsBuilder<SalesDayDbContext>().UseSqlServer(connection).Options);
        await using var menu = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(connection).Options);
        await day.Database.UseTransactionAsync(transaction); await menu.Database.UseTransactionAsync(transaction);
        Assert.True(await Lock(connection, transaction, "Shared", 0) >= 0);
        day.Days.Add(new() { Id = Guid.NewGuid(), BusinessDate = new(2026, 11, 9), OpenedBy = Guid.NewGuid(), RequestKey = Guid.NewGuid() });
        menu.Add(new NhomMon { Id = Guid.NewGuid(), Ten = "Rollback" });
        await day.SaveChangesAsync(); await menu.SaveChangesAsync();
        await transaction.RollbackAsync();
        await using var verifyDay = Day(); await using var verifyMenu = Menu();
        Assert.Empty(await verifyDay.Days.ToListAsync());
        Assert.Empty(await verifyMenu.Set<NhomMon>().ToListAsync());
    }

    // ADR-01 review (Phuong): the replay check above only runs when the first request wins the race.
    [SqlServerFact]
    public async Task Replay_with_the_same_key_returns_the_original_day_and_other_payloads_conflict()
    {
        var actor = Guid.NewGuid();
        var key = Guid.NewGuid();
        var request = new OpenDayRequest(new DateOnly(2026, 11, 9), 100000);
        await using var db = Day();
        var service = new BusinessDays(db);

        var first = await service.OpenAsync(request, actor, key, default);
        var replay = await service.OpenAsync(request, actor, key, default);

        Assert.Null(first.Error);
        Assert.False(first.Replayed);
        Assert.Null(replay.Error);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Day!.Id, replay.Day!.Id);
        Assert.Equal("salesday.idempotency.conflict", (await service.OpenAsync(request with { OpeningCash = 1 }, actor, key, default)).Error);
        Assert.Equal("salesday.idempotency.conflict", (await service.OpenAsync(request with { BusinessDate = request.BusinessDate.AddDays(1) }, actor, key, default)).Error);
        Assert.Equal("salesday.idempotency.conflict", (await service.OpenAsync(request, Guid.NewGuid(), key, default)).Error);
        await using var read = Day();
        var stored = Assert.Single(await read.Days.ToListAsync());
        Assert.Equal(key, stored.RequestKey);
        Assert.Equal(request.OpeningCash, stored.OpeningCash);
    }

    [SqlServerFact]
    public async Task Concurrent_retries_with_the_same_key_create_exactly_one_day()
    {
        var actor = Guid.NewGuid();
        var key = Guid.NewGuid();
        var request = new OpenDayRequest(new DateOnly(2026, 11, 9), 250000);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = Day();
            return await new BusinessDays(db).OpenAsync(request, actor, key, default);
        }));

        Assert.All(results, x => Assert.Null(x.Error));
        Assert.Single(results, x => !x.Replayed);
        Assert.Single(results.Select(x => x.Day!.Id).Distinct());
        await using var read = Day();
        Assert.Equal(1, await read.Days.CountAsync());
    }

    // The coexistence test above only uses LockTimeout=0; this one proves Exclusive really waits.
    [SqlServerFact]
    public async Task C03_exclusive_waits_for_a_shared_holder_and_is_granted_after_release()
    {
        await using var holder = new SqlConnection(connectionString);
        await holder.OpenAsync();
        await using var closer = new SqlConnection(connectionString);
        await closer.OpenAsync();
        await using var shared = (SqlTransaction)await holder.BeginTransactionAsync();
        await using var exclusive = (SqlTransaction)await closer.BeginTransactionAsync();
        Assert.True(await Lock(holder, shared, "Shared", 0) >= 0);

        var timer = Stopwatch.StartNew();
        Assert.Equal(-1, await Lock(closer, exclusive, "Exclusive", 1500));
        Assert.True(timer.ElapsedMilliseconds >= 1000, $"Exclusive returned after {timer.ElapsedMilliseconds} ms instead of waiting for the timeout.");

        var waiting = Lock(closer, exclusive, "Exclusive", 15000);
        await Task.Delay(500);
        Assert.False(waiting.IsCompleted, "Exclusive must keep waiting while a Shared lock is held.");
        await shared.RollbackAsync();
        // sp_getapplock returns 1 when the lock is granted after waiting for incompatible locks.
        Assert.Equal(1, await waiting.WaitAsync(TimeSpan.FromSeconds(15)));
    }

    [SqlServerFact]
    public async Task C03_failure_in_the_second_module_rolls_back_the_first_module_write()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var day = new SalesDayDbContext(new DbContextOptionsBuilder<SalesDayDbContext>().UseSqlServer(connection).Options);
        await using var menu = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(connection).Options);
        await day.Database.UseTransactionAsync(transaction);
        await menu.Database.UseTransactionAsync(transaction);
        Assert.True(await Lock(connection, transaction, "Shared", 0) >= 0);

        day.Days.Add(new() { Id = Guid.NewGuid(), BusinessDate = new(2026, 11, 9), OpenedBy = Guid.NewGuid(), RequestKey = Guid.NewGuid() });
        await day.SaveChangesAsync();
        menu.Add(new Mon { Id = Guid.NewGuid(), NhomMonId = Guid.NewGuid(), Ma = "ORPHAN", Ten = "Missing group" });
        await Assert.ThrowsAsync<DbUpdateException>(() => menu.SaveChangesAsync());
        await transaction.RollbackAsync();

        await using var verifyDay = Day();
        await using var verifyMenu = Menu();
        Assert.Empty(await verifyDay.Days.ToListAsync());
        Assert.Empty(await verifyMenu.Set<Mon>().ToListAsync());
    }
}
