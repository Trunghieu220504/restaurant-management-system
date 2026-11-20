using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Contracts;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Shared.SqlServer;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PhuongWeekTwoSqlServerTests : IAsyncLifetime
{
    private readonly string database = "RestaurantPhuongW2_" + Guid.NewGuid().ToString("N");
    private ServiceProvider? provider;

    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(supplied)) return;
        var connection = new SqlConnectionStringBuilder(supplied) { InitialCatalog = database }.ConnectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Restaurant"] = connection }).Build();
        provider = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddLogging().AddRestaurantModules().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SalesDayDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (provider is null) return;
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        if (db.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Unexpected test database");
        await db.Database.EnsureDeletedAsync();
        await provider.DisposeAsync();
    }

    [SqlServerFact]
    public async Task One_closer_wins_and_a_waiting_writer_reads_the_committed_day_state()
    {
        var services = provider!;
        await using var owner = services.CreateAsyncScope();
        await using var contender = services.CreateAsyncScope();
        await using var writer = services.CreateAsyncScope();
        var first = owner.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        var second = contender.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        var late = writer.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        var gate = writer.ServiceProvider.GetRequiredService<ISalesDayGate>();
        var day = new BusinessDay
        {
            Id = Guid.NewGuid(),
            BusinessDate = new(2026, 11, 20),
            OpenedAt = DateTimeOffset.UtcNow,
            OpenedBy = Guid.NewGuid(),
            RequestKey = Guid.NewGuid()
        };
        first.Days.Add(day);
        await first.SaveChangesAsync();

        await using (var probe = await late.Database.BeginTransactionAsync())
        {
            Assert.Equal(day.BusinessDate, (await gate.RequireOpenAsync(day.Id, default)).Day!.BusinessDate);
            Assert.Equal(SalesDayErrorCodes.DayNotFound, (await gate.RequireOpenAsync(Guid.NewGuid(), default)).ErrorCode);
            await probe.RollbackAsync();
        }

        await using var firstTx = await first.Database.BeginTransactionAsync();
        await using var secondTx = await second.Database.BeginTransactionAsync();
        await using var writerTx = await late.Database.BeginTransactionAsync();
        async Task<int> FreezeAsync(SalesDayDbContext db)
        {
            await BusinessDayLock.AcquireAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction!.GetDbTransaction(),
                day.Id, BusinessDayLockMode.Exclusive);
            return await db.Days.Where(x => x.Id == day.Id && x.Status == SalesDayStatuses.Open)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, SalesDayStatuses.PendingClose));
        }

        Assert.Equal(1, await FreezeAsync(first));
        var competingClose = FreezeAsync(second);
        var lateWrite = gate.RequireOpenAsync(day.Id, default);
        await Task.Delay(150);
        Assert.False(competingClose.IsCompleted);
        Assert.False(lateWrite.IsCompleted);
        await firstTx.CommitAsync();

        // Each contender releases its transaction as soon as it returns, independent of SQL's grant order.
        async Task<int> FinishCloseAsync()
        {
            var rows = await competingClose;
            await secondTx.CommitAsync();
            return rows;
        }
        async Task<string?> FinishWriteAsync()
        {
            var result = await lateWrite;
            await writerTx.RollbackAsync();
            return result.ErrorCode;
        }
        var close = FinishCloseAsync();
        var write = FinishWriteAsync();
        await Task.WhenAll(close, write).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(0, await close);
        Assert.Equal(SalesDayErrorCodes.DayNotOpen, await write);
    }

    [SqlServerFact]
    public async Task Seating_holds_the_real_shared_gate_until_its_own_transaction_ends()
    {
        await using var scope = provider!.CreateAsyncScope();
        var days = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        var seating = scope.ServiceProvider.GetRequiredService<SeatingDbContext>();
        var directory = scope.ServiceProvider.GetRequiredService<IBusinessDayDirectory>();
        var day = new BusinessDay
        {
            Id = Guid.NewGuid(),
            BusinessDate = new(2026, 11, 20),
            OpenedAt = DateTimeOffset.UtcNow,
            OpenedBy = Guid.NewGuid(),
            RequestKey = Guid.NewGuid()
        };
        days.Days.Add(day);
        await days.SaveChangesAsync();
        await using var writing = await seating.Database.BeginTransactionAsync();
        Assert.Equal(day.Id, await directory.CurrentOpenDayAsync(default));
        Assert.True(await directory.RequireOpenAsync(writing.GetDbTransaction(), day.Id, default));
        await using var closing = await days.Database.BeginTransactionAsync();
        Task TakeExclusive() => BusinessDayLock.AcquireAsync(days.Database.GetDbConnection(), closing.GetDbTransaction(),
            day.Id, BusinessDayLockMode.Exclusive, 0);
        await Assert.ThrowsAsync<TimeoutException>(TakeExclusive);
        await writing.CommitAsync();
        await TakeExclusive();
        await closing.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Legacy_dish_without_variants_can_be_repaired_after_migration()
    {
        await using var scope = provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.GetService<IMigrator>().MigrateAsync("20261110074315_InitialCatalog");
        var group = new NhomMon { Id = Guid.NewGuid(), Ten = "Phở" };
        var dish = new Mon { Id = Guid.NewGuid(), NhomMonId = group.Id, Ma = "PHO", Ten = "Phở bò", Tram = "BEP" };
        db.AddRange(group, dish);
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var catalog = new CatalogMenu(db);
        var old = Assert.Single((await catalog.GetMenuAsync(default)).Items);
        Assert.False(old.Active);
        var activate = new UpdateMenuItemRequest(group.Id, dish.Ma, dish.Ten, dish.Tram, true, old.Version);
        Assert.Equal(CatalogErrors.VariantRequired, (await catalog.UpdateItemAsync(dish.Id, activate, default)).Error);
        Assert.Null((await catalog.CreateVariantAsync(dish.Id, new("STD", "Tiêu chuẩn", 55000), default)).Error);
        Assert.True((await catalog.UpdateItemAsync(dish.Id, activate, default)).Value!.Active);
    }

    [SqlServerFact]
    public async Task InitialBilling_applies_once_and_rejects_duplicate_charges_and_invalid_money_or_documents()
    {
        await using var scope = provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await db.Database.MigrateAsync();
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(), id => id.EndsWith("_InitialBilling", StringComparison.Ordinal));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var sql = db.Database;
        await sql.ExecuteSqlRawAsync("""
            INSERT billing.HoaDon (DonHangId, MaHoaDon, NgayBanLapId, TaoLuc) VALUES (1, 'HD1', NEWID(), SYSUTCDATETIME());
            INSERT billing.DongHoaDon (HoaDonId, ChiTietDonId, TenMonChot, ThanhTienGoc) VALUES (1, 42, N'Phở', 50000);
            INSERT billing.ThanhToan (HoaDonId, NgayBanId, PhuongThuc, SoTien, TrangThai, TienKhachDua, TienThua, NguoiThuId, TaoLuc, KhoaYeuCau)
                VALUES (1, NEWID(), 'TIEN_MAT', 50000, 'THANH_CONG', 60000, 10000, NEWID(), SYSUTCDATETIME(), NEWID());
            INSERT billing.HoanTien (ThanhToanId, NgayBanId, SoTien, PhuongThuc, TrangThai, LyDo, NguoiDeNghiId, TaoLuc, KhoaYeuCau)
                VALUES (1, NEWID(), 1000, 'TIEN_MAT', 'CHO_DUYET', N'Test', NEWID(), SYSUTCDATETIME(), NEWID());
            INSERT billing.DieuChinhNghiaVu (HoaDonId, SoTien, LyDo, NgayBanId, NguoiDuyetId, TaoLuc, KhoaYeuCau)
                VALUES (1,-1000,N'Test',NEWID(),NEWID(),SYSUTCDATETIME(),NEWID());
            INSERT billing.MienThu (HoaDonId, SoTien, LyDo, NgayBanId, NguoiDuyetId, TaoLuc, KhoaYeuCau)
                VALUES (1,1000,N'Test',NEWID(),NEWID(),SYSUTCDATETIME(),NEWID());
            INSERT billing.ChungTu (HoaDonId,SoChungTu,Loai,ThanhToanId,HoanTienId,NoiDungChot,TaoLuc,NguoiLapId)
                VALUES (1,'HD','HOA_DON',NULL,NULL,N'HD',SYSUTCDATETIME(),NEWID()),
                       (1,'PT','PHIEU_THU',1,NULL,N'PT',SYSUTCDATETIME(),NEWID()),
                       (1,'PH','PHIEU_HOAN',NULL,1,N'PH',SYSUTCDATETIME(),NEWID());
            """);
        foreach (var duplicate in new[] {
            "INSERT billing.HoaDon (DonHangId,MaHoaDon,NgayBanLapId,TaoLuc) VALUES (1,'HD2',NEWID(),SYSUTCDATETIME())",
            "INSERT billing.DongHoaDon (HoaDonId,ChiTietDonId,TenMonChot,ThanhTienGoc) VALUES (1,42,N'Test',1)" })
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => sql.ExecuteSqlRawAsync(duplicate));
            Assert.Contains(error.Number, new[] { 2601, 2627 });
        }
        foreach (var invalid in new[] {
            "UPDATE billing.DieuChinhNghiaVu SET SoTien=0", "UPDATE billing.MienThu SET SoTien=0",
            "UPDATE billing.ThanhToan SET TienThua=NULL", "UPDATE billing.ThanhToan SET PhuongThuc='CHUYEN_KHOAN'",
            "UPDATE billing.HoanTien SET SoTien=-1", "UPDATE billing.ChungTu SET Loai='PHIEU_HOAN' WHERE SoChungTu='PT'",
            "UPDATE billing.ChungTu SET ThanhToanId=NULL WHERE SoChungTu='PT'" })
            Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => sql.ExecuteSqlRawAsync(invalid))).Number);
    }
}
