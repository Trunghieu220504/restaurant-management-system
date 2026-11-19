using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Modules.Seating.Domain;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class SeatingSqlServerTests : IAsyncLifetime
{
    private readonly string databaseName = "RestaurantSeatingTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    private SeatingDbContext CreateContext() => new(new DbContextOptionsBuilder<SeatingDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "seating")).Options);

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
            !databaseName.StartsWith("RestaurantSeatingTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public void Model_and_migrations_define_all_tables()
    {
        using var db = new SeatingDbContext(new DbContextOptionsBuilder<SeatingDbContext>()
            .UseSqlServer(ApiFactory.ConnectionString).Options);
        Assert.Equal(6, db.Model.GetEntityTypes().Count());
        Assert.Equal(2, db.Database.GetMigrations().Count());
        Assert.All(db.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()),
            fk => Assert.Equal("seating", fk.PrincipalEntityType.GetSchema()));
        Assert.Contains("WHERE [DongLuc] IS NULL", db.Database.GenerateCreateScript());
    }

    private async Task SeedTablesAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO seating.KhuVuc (Ma, Ten, HoatDong) VALUES ('A', N'Area', 1);
            INSERT INTO seating.Ban (KhuVucId, Ma, Ten, HoatDong, MaQrCongKhai)
                VALUES (1, 'T1', N'Table 1', 1, 'qr-1'), (1, 'T2', N'Table 2', 1, 'qr-2');
            """);
    }

    private static Task<int> OpenAsync(SeatingDbContext db) => db.Database.ExecuteSqlRawAsync("""
        INSERT INTO seating.PhienSuDung (BanHienTaiId, NgayBanId, MoLuc, NguoiMoId)
        VALUES (1, '22222222-2222-2222-2222-222222222222', SYSUTCDATETIME(), '11111111-1111-1111-1111-111111111111');
        """);

    [SqlServerFact]
    public async Task Concurrent_opens_allow_exactly_one_active_session()
    {
        await SeedTablesAsync();
        async Task<bool> Attempt()
        {
            await using var db = CreateContext();
            try { await OpenAsync(db); return true; }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, success => success);
        Assert.Single(results, success => !success);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM seating.PhienSuDung WHERE DongLuc IS NULL").SingleAsync());
    }

    [SqlServerFact]
    public async Task Closed_session_releases_table_without_deleting_history()
    {
        await SeedTablesAsync();
        await using var db = CreateContext();
        await OpenAsync(db);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE seating.PhienSuDung SET DongLuc=SYSUTCDATETIME(),
                NguoiDongId='11111111-1111-1111-1111-111111111111';
            """);
        await OpenAsync(db);
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM seating.PhienSuDung").SingleAsync());
    }

    [SqlServerFact]
    public async Task Guest_grant_remains_bound_to_session_after_table_transfer()
    {
        await SeedTablesAsync();
        await using var db = CreateContext();
        await OpenAsync(db);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO seating.QuyenTruyCapKhach (Id, PhienSuDungId, TokenHash, TaoLuc, HetHanLuc)
                VALUES (NEWID(), 1, HASHBYTES('SHA2_256', 'token'), SYSUTCDATETIME(), DATEADD(hour, 1, SYSUTCDATETIME()));
            INSERT INTO seating.ChuyenBan (PhienSuDungId, BanCuId, BanMoiId, ChuyenLuc, NguoiChuyenId, LyDo)
                VALUES (1, 1, 2, SYSUTCDATETIME(), '11111111-1111-1111-1111-111111111111', N'Move');
            UPDATE seating.PhienSuDung SET BanHienTaiId=2 WHERE Id=1;
            """);
        Assert.Equal(2L, await db.Database.SqlQueryRaw<long>("""
            SELECT p.BanHienTaiId AS [Value] FROM seating.QuyenTruyCapKhach g
            JOIN seating.PhienSuDung p ON p.Id=g.PhienSuDungId WHERE g.ThuHoiLuc IS NULL
            """).SingleAsync());
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM seating.PhienSuDung WHERE Id=1"));
        Assert.Equal(547, error.Number);
    }

    [SqlServerFact]
    public async Task Move_to_same_table_is_rejected()
    {
        await SeedTablesAsync();
        await using var db = CreateContext();
        await OpenAsync(db);
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO seating.ChuyenBan (PhienSuDungId, BanCuId, BanMoiId, ChuyenLuc, NguoiChuyenId, LyDo)
            VALUES (1, 1, 1, SYSUTCDATETIME(), '11111111-1111-1111-1111-111111111111', N'Move');
            """));
        Assert.Equal(547, error.Number);
    }

    [SqlServerFact]
    public async Task Rowversion_rejects_stale_table_update()
    {
        await SeedTablesAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        var current = await first.Set<Ban>().SingleAsync(x => x.Id == 1);
        var stale = await second.Set<Ban>().SingleAsync(x => x.Id == 1);
        Assert.Equal(8, current.Version.Length);
        current.Ten = "Updated";
        await first.SaveChangesAsync();
        stale.Ten = "Stale overwrite";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verification = CreateContext();
        Assert.Equal("Updated", (await verification.Set<Ban>().SingleAsync(x => x.Id == 1)).Ten);
    }

    [SqlServerFact]
    public async Task Migration_reapply_preserves_data_and_down_up_is_supported()
    {
        await SeedTablesAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM seating.Ban").SingleAsync());
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("0");
        await migrator.MigrateAsync();
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM seating.Ban").SingleAsync());
    }
}
