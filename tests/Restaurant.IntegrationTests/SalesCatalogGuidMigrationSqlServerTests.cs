using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>
/// SalesCatalogGuidIds replaces the four bigint catalog columns with uniqueidentifier. It must work on an empty
/// database, keep the key and indexes, be reversible, and refuse (not corrupt) a database that already holds rows.
/// </summary>
public sealed class SalesCatalogGuidMigrationSqlServerTests : IAsyncLifetime
{
    private const string Initial = "20261118103333_InitialSales";
    private const string Target = "20261119035517_SalesCatalogGuidIds";

    private readonly string databaseName = "RestaurantSalesTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    private SalesDbContext CreateContext() => new(new DbContextOptionsBuilder<SalesDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options);

    public Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(supplied))
            connectionString = new SqlConnectionStringBuilder(supplied) { InitialCatalog = databaseName }.ConnectionString;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        if (!databaseName.StartsWith("RestaurantSalesTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [SqlServerFact]
    public async Task Up_converts_the_columns_keeps_key_and_indexes_and_down_restores_bigint()
    {
        await using var db = CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Initial);
        Assert.Equal("bigint", await ColumnTypeAsync(db, "ChiTietDon", "MonId"));

        await migrator.MigrateAsync(Target);
        foreach (var (table, column) in new[]
                 {
                     ("ChiTietDon", "MonId"), ("ChiTietDon", "BienTheId"),
                     ("TuyChonDaDat", "TuyChonId"), ("TuyChonDaDat", "NhomTuyChonId")
                 })
            Assert.Equal("uniqueidentifier", await ColumnTypeAsync(db, table, column));
        Assert.Equal(3, await ScalarAsync(db, """
            SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_ChiTietDon_MonId','IX_ChiTietDon_BienTheId','IX_TuyChonDaDat_TuyChonId')
            """));
        Assert.Equal(2, await ScalarAsync(db, """
            SELECT COUNT(*) FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            WHERE i.name = 'PK_TuyChonDaDat'
            """));

        await migrator.MigrateAsync(Initial);
        Assert.Equal("bigint", await ColumnTypeAsync(db, "TuyChonDaDat", "TuyChonId"));
    }

    [SqlServerFact]
    public async Task Up_refuses_to_run_when_order_details_already_exist()
    {
        await using var db = CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Initial);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO sales.DonHang (MaDon, NgayBanId, Loai, PhienSuDungId, NhanBanLucTao, TrangThai, TaoLuc, SoLanCapNhat)
            VALUES ('DH-1', NEWID(), 'TAI_BAN', 1, N'T1', 'MO', SYSUTCDATETIME(), 0);
            INSERT INTO sales.DotGoiMon (DonHangId, SoDot, Nguon, NguoiTaoId, TrangThai, TaoLuc, KhoaYeuCau)
            VALUES (SCOPE_IDENTITY(), 1, 'NHAN_VIEN', NEWID(), 'CHO_XAC_NHAN', SYSUTCDATETIME(), NEWID());
            INSERT INTO sales.ChiTietDon (DotGoiMonId, MonId, BienTheId, TenMonChot, TenBienTheChot, GiaMonChot, TramChot, TrangThai)
            VALUES (SCOPE_IDENTITY(), 1, 2, N'Dish', N'Variant', 1000, 'BEP', 'CHO_XAC_NHAN');
            """);

        var failure = await Assert.ThrowsAnyAsync<SqlException>(() => migrator.MigrateAsync(Target));
        Assert.Contains("already hold rows", failure.Message);
        Assert.Equal("bigint", await ColumnTypeAsync(db, "ChiTietDon", "MonId"));
    }

    private static Task<string> ColumnTypeAsync(SalesDbContext db, string table, string column) =>
        db.Database.SqlQueryRaw<string>("""
            SELECT t.name AS [Value] FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(@table) AND c.name = @column
            """, new SqlParameter("@table", "sales." + table), new SqlParameter("@column", column)).SingleAsync();

    private static Task<int> ScalarAsync(SalesDbContext db, string sql) =>
        db.Database.SqlQueryRaw<int>(sql.Replace("COUNT(*)", "COUNT(*) AS [Value]", StringComparison.Ordinal)).SingleAsync();
}
