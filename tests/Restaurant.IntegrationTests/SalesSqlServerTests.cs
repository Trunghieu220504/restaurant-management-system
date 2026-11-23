using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Sales.Domain;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

/// <summary>HIE-2.2 on a real SQL Server: InitialSales migration, the one-open-order-per-session index and constraints.</summary>
public sealed class SalesSqlServerTests : IAsyncLifetime
{
    private static readonly Guid Day = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Staff = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Grant = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    private readonly string databaseName = "RestaurantSalesTest_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    private SalesDbContext CreateContext() => new(new DbContextOptionsBuilder<SalesDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sales")).Options);

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
            !databaseName.StartsWith("RestaurantSalesTest_", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public void Model_and_migration_define_the_sales_tables_without_cross_module_keys_or_cascades()
    {
        using var db = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(ApiFactory.ConnectionString).Options);
        Assert.Equal(5, db.Model.GetEntityTypes().Count()); // four business tables plus the technical Outbox (HIE-3.0)
        Assert.Equal(
            ["20261118103333_InitialSales", "20261119025424_AddSalesOutbox", "20261119035517_SalesCatalogGuidIds"],
            db.Database.GetMigrations());
        Assert.All(db.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()), fk =>
        {
            Assert.Equal("sales", fk.PrincipalEntityType.GetSchema());
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        });
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("WHERE [TrangThai] = 'MO' AND [PhienSuDungId] IS NOT NULL", script);
        Assert.DoesNotContain("CASCADE", script);
    }

    // ---- D-10: one open order per session ----

    [SqlServerFact]
    public async Task Concurrent_orders_for_one_session_allow_exactly_one_open_order()
    {
        async Task<bool> Attempt(string code)
        {
            await using var db = CreateContext();
            try
            {
                await db.Database.ExecuteSqlRawAsync(OrderSql(code, "TAI_BAN", "7"));
                return true;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(Attempt("DH-A"), Attempt("DH-B"), Attempt("DH-C"));
        Assert.Single(results, success => success);
        Assert.Equal(2, results.Count(success => !success));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM sales.DonHang WHERE PhienSuDungId = 7 AND TrangThai = 'MO'"));
    }

    [SqlServerFact]
    public async Task A_session_can_have_a_new_order_after_the_previous_one_is_closed_or_cancelled()
    {
        await ExecAsync(OrderSql("DH-1", "TAI_BAN", "7"));
        Assert.Equal(2601, await ErrorAsync(OrderSql("DH-2", "TAI_BAN", "7")));
        await ExecAsync("UPDATE sales.DonHang SET TrangThai = 'DONG', DongLuc = SYSUTCDATETIME() WHERE MaDon = 'DH-1'");
        await ExecAsync(OrderSql("DH-2", "TAI_BAN", "7"));
        await ExecAsync("UPDATE sales.DonHang SET TrangThai = 'HUY', DongLuc = SYSUTCDATETIME() WHERE MaDon = 'DH-2'");
        await ExecAsync(OrderSql("DH-3", "TAI_BAN", "7"));
        Assert.Equal(3, await CountAsync("SELECT COUNT(*) FROM sales.DonHang WHERE PhienSuDungId = 7"));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM sales.DonHang WHERE PhienSuDungId = 7 AND TrangThai = 'MO'"));
    }

    [SqlServerFact]
    public async Task Different_sessions_and_any_number_of_takeaway_orders_can_be_open_together()
    {
        await ExecAsync(OrderSql("DH-1", "TAI_BAN", "7"));
        await ExecAsync(OrderSql("DH-2", "TAI_BAN", "8"));
        await ExecAsync(OrderSql("DH-3", "MANG_DI", "NULL"));
        await ExecAsync(OrderSql("DH-4", "MANG_DI", "NULL"));
        await ExecAsync(OrderSql("DH-5", "MANG_DI", "NULL"));
        Assert.Equal(5, await CountAsync("SELECT COUNT(*) FROM sales.DonHang WHERE TrangThai = 'MO'"));
    }

    // ---- constraints ----

    [SqlServerFact]
    public async Task An_order_must_match_its_type_status_and_closing_time()
    {
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-1", "TAI_BAN", "NULL")));
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-2", "MANG_DI", "7")));
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-3", "TAI_CHO", "7")));
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-4", "MANG_DI", "NULL", "'CHO'")));
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-5", "MANG_DI", "NULL", "'DONG'")));
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-6", "MANG_DI", "NULL", "'MO'", "SYSUTCDATETIME()")));
        Assert.Equal(547, await ErrorAsync(OrderSql("DH-7", "MANG_DI", "NULL", "'DONG'", "DATEADD(day, -1, SYSUTCDATETIME())")));
        await ExecAsync(OrderSql("DH-8", "MANG_DI", "NULL", "'DONG'", "DATEADD(minute, 1, SYSUTCDATETIME())"));
        Assert.Equal(2601, await ErrorAsync(OrderSql("DH-8", "MANG_DI", "NULL")));
    }

    [SqlServerFact]
    public async Task A_batch_must_match_its_source_status_and_decision()
    {
        await ExecAsync(OrderSql("DH-1", "TAI_BAN", "7"));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'KHACH'", "NULL", "NULL")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'KHACH'", $"'{Grant}'", $"'{Staff}'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", "NULL")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", $"'{Grant}'", $"'{Staff}'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'ROBOT'", "NULL", $"'{Staff}'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 0, "'NHAN_VIEN'", "NULL", $"'{Staff}'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'DA_XAC_NHAN'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'DA_TU_CHOI'", decided: true)));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'CHO_XAC_NHAN'", decided: true)));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'DA_XAC_NHAN'", decided: true, reason: "N'x'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'KHAC'")));
        Assert.Equal(547, await ErrorAsync(BatchSql(99, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'")));

        await ExecAsync(BatchSql(1, 1, "'KHACH'", $"'{Grant}'", "NULL"));
        await ExecAsync(BatchSql(1, 2, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'DA_XAC_NHAN'", decided: true));
        await ExecAsync(BatchSql(1, 3, "'NHAN_VIEN'", "NULL", $"'{Staff}'", "'DA_TU_CHOI'", decided: true, reason: "N'Sold out'"));
    }

    [SqlServerFact]
    public async Task Batch_numbers_are_unique_per_order_and_request_keys_are_unique_everywhere()
    {
        await ExecAsync(OrderSql("DH-1", "TAI_BAN", "7"));
        await ExecAsync(OrderSql("DH-2", "TAI_BAN", "8"));
        var key = Guid.NewGuid();
        await ExecAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'", key: key));
        Assert.Equal(2601, await ErrorAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'")));
        Assert.Equal(2601, await ErrorAsync(BatchSql(2, 2, "'NHAN_VIEN'", "NULL", $"'{Staff}'", key: key)));
        await ExecAsync(BatchSql(2, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'"));
    }

    [SqlServerFact]
    public async Task Portions_and_options_enforce_station_price_and_one_selection_per_option()
    {
        await ExecAsync(OrderSql("DH-1", "TAI_BAN", "7"));
        await ExecAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'"));
        Assert.Equal(547, await ErrorAsync(PortionSql(1, "'KHO'", 1000)));
        Assert.Equal(547, await ErrorAsync(PortionSql(1, "'BEP'", -1)));
        Assert.Equal(547, await ErrorAsync(PortionSql(1, "'BEP'", 1000, "'DA_GIAO'")));
        Assert.Equal(547, await ErrorAsync(PortionSql(99, "'BEP'", 1000)));
        await ExecAsync(PortionSql(1, "'BAR'", 0));

        Assert.Equal(547, await ErrorAsync(OptionSql(LastPortion, 5, "'KHAC'", 0)));
        Assert.Equal(547, await ErrorAsync(OptionSql(LastPortion, 5, "'TOPPING'", -1)));
        Assert.Equal(547, await ErrorAsync(OptionSql("99", 5, "'TOPPING'", 0)));
        await ExecAsync(OptionSql(LastPortion, 5, "'TOPPING'", 5000));
        Assert.Equal(2627, await ErrorAsync(OptionSql(LastPortion, 5, "'TOPPING'", 5000)));
        await ExecAsync(OptionSql(LastPortion, 6, "'TUY_CHON'", 0));
    }

    [SqlServerFact]
    public async Task Order_history_cannot_be_deleted_through_its_parents()
    {
        await ExecAsync(OrderSql("DH-1", "TAI_BAN", "7"));
        await ExecAsync(BatchSql(1, 1, "'NHAN_VIEN'", "NULL", $"'{Staff}'"));
        await ExecAsync(PortionSql(1, "'BEP'", 1000));
        await ExecAsync(OptionSql(LastPortion, 5, "'TOPPING'", 0));
        Assert.Equal(547, await ErrorAsync("DELETE FROM sales.ChiTietDon"));
        Assert.Equal(547, await ErrorAsync("DELETE FROM sales.DotGoiMon"));
        Assert.Equal(547, await ErrorAsync("DELETE FROM sales.DonHang"));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM sales.DonHang"));
    }

    // ---- the domain model through EF ----

    [SqlServerFact]
    public async Task An_order_with_batches_portions_and_options_round_trips_with_its_snapshots()
    {
        long orderId;
        await using (var db = CreateContext())
        {
            var order = DonHang.Create("DH-1", OrderTypes.DineIn, Day, 7, "Table 7", null, Staff, Now).Value!;
            order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [
                new NewPortion(CatalogId.Of(11), CatalogId.Of(22), "Milk tea", "Large", 45000, Stations.Bar, "less ice", [
                    new NewOption(CatalogId.Of(3), CatalogId.Of(30), "Toppings", OptionGroupKinds.Topping, "Pearl", 5000),
                    new NewOption(CatalogId.Of(4), CatalogId.Of(31), "Sugar", OptionGroupKinds.Option, "50%", 0)]),
                new NewPortion(CatalogId.Of(12), CatalogId.Of(23), "Pho", "Regular", 60000, Stations.Kitchen, null)], Now);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var read = CreateContext();
        var loaded = await read.Orders.Include(x => x.Dots).ThenInclude(x => x.Portions).ThenInclude(x => x.Options)
            .SingleAsync(x => x.Id == orderId);
        Assert.Equal((OrderTypes.DineIn, 7L, "Table 7"), (loaded.Loai, loaded.PhienSuDungId, loaded.NhanBanLucTao));
        var batch = Assert.Single(loaded.Dots);
        Assert.Equal((1, BatchStatus.Pending, BatchSource.Staff), (batch.SoDot, batch.TrangThai, batch.Nguon));
        var tea = batch.Portions.Single(x => x.TenMonChot == "Milk tea");
        Assert.Equal((CatalogId.Of(22), 45000L, Stations.Bar, "less ice"), (tea.BienTheId, tea.GiaMonChot, tea.TramChot, tea.GhiChu));
        Assert.Equal(2, tea.Options.Count);
        Assert.Contains(tea.Options, o => o.TuyChonId == CatalogId.Of(3) && o.TenNhomChot == "Toppings" && o.GiaCongChot == 5000 &&
                                          o.LoaiNhomChot == OptionGroupKinds.Topping);
        Assert.Equal(2, batch.Portions.Count);
        Assert.NotEmpty(loaded.Version);
    }

    [SqlServerFact]
    public async Task The_whole_lifecycle_is_saved_and_a_closed_order_refuses_new_batches_after_reload()
    {
        long orderId;
        await using (var db = CreateContext())
        {
            var order = DonHang.Create("DH-1", OrderTypes.DineIn, Day, 7, null, null, Staff, Now).Value!;
            order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Burger(), Burger()], Now);
            order.AddBatch(BatchSource.Guest, Grant, null, Guid.NewGuid(), [Burger()], Now);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var db = CreateContext())
        {
            var order = await Load(db, orderId);
            Assert.True(order.ConfirmBatch(1, Staff, Now.AddMinutes(1)).Succeeded);
            Assert.True(order.RejectBatch(2, Staff, "Sold out", Now.AddMinutes(1)).Succeeded);
            Assert.True(order.CancelPortion(1, order.Dots[0].Portions[0].Id).Succeeded);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var order = await Load(db, orderId);
            Assert.Equal(BatchStatus.Confirmed, order.Dots[0].TrangThai);
            Assert.Equal(new[] { PortionStatus.Cancelled, PortionStatus.Confirmed },
                order.Dots[0].Portions.Select(x => x.TrangThai).Order().ToArray());
            Assert.Equal(("Sold out", Staff), (order.Dots[1].LyDoTuChoi, order.Dots[1].NguoiXacNhanId!.Value));
            Assert.True(order.Close(Now.AddHours(1)).Succeeded);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var order = await Load(db, orderId);
            Assert.Equal(OrderStatus.Closed, order.TrangThai);
            var refused = order.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Burger()], Now.AddHours(2));
            Assert.Equal(SalesErrors.OrderNotOpen, refused.Error);
            await db.SaveChangesAsync();
        }

        Assert.Equal(2, await CountAsync("SELECT COUNT(*) FROM sales.DotGoiMon"));
        Assert.Equal(3, await CountAsync("SELECT COUNT(*) FROM sales.ChiTietDon"));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM sales.DonHang WHERE TrangThai = 'DONG' AND DongLuc IS NOT NULL"));
    }

    [SqlServerFact]
    public async Task Two_people_changing_the_same_order_cannot_both_win()
    {
        long orderId;
        await using (var db = CreateContext())
        {
            var order = DonHang.Create("DH-1", OrderTypes.Takeaway, Day, null, null, "An", Staff, Now).Value!;
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var closing = await Load(first, orderId);
        var cancelling = await Load(second, orderId);
        Assert.True(closing.Close(Now.AddMinutes(5)).Succeeded);
        Assert.True(cancelling.Cancel(Now.AddMinutes(5)).Succeeded);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM sales.DonHang WHERE TrangThai = 'DONG'"));
    }

    [SqlServerFact]
    public Task Adding_a_batch_then_closing_at_the_same_time_cannot_both_win() => AddAndCloseRace(adderSavesFirst: true);

    [SqlServerFact]
    public Task Closing_then_adding_a_batch_at_the_same_time_cannot_both_win() => AddAndCloseRace(adderSavesFirst: false);

    private async Task AddAndCloseRace(bool adderSavesFirst)
    {
        long orderId;
        await using (var db = CreateContext())
        {
            var order = DonHang.Create("DH-1", OrderTypes.Takeaway, Day, null, null, "An", Staff, Now).Value!;
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var adder = CreateContext();
        await using var closer = CreateContext();
        var adding = await Load(adder, orderId);
        var closing = await Load(closer, orderId);
        Assert.True(adding.AddBatch(BatchSource.Staff, null, Staff, Guid.NewGuid(), [Burger()], Now.AddMinutes(1)).Succeeded);
        Assert.True(closing.Close(Now.AddMinutes(2)).Succeeded);

        var (firstSaver, secondSaver) = adderSavesFirst ? (adder, closer) : (closer, adder);
        await firstSaver.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondSaver.SaveChangesAsync());

        // Whatever won, a closed order never carries a batch that waits for confirmation.
        Assert.Equal(0, await CountAsync(
            "SELECT COUNT(*) FROM sales.DonHang d JOIN sales.DotGoiMon b ON b.DonHangId = d.Id "
            + "WHERE d.TrangThai = 'DONG' AND b.TrangThai = 'CHO_XAC_NHAN'"));
    }

    [SqlServerFact]
    public async Task A_takeaway_order_cannot_carry_a_table_label()
    {
        Assert.Equal(547, await ErrorAsync(
            "INSERT INTO sales.DonHang (MaDon, NgayBanId, Loai, PhienSuDungId, NhanBanLucTao, TrangThai, TaoLuc) "
            + $"VALUES ('DH-1', '{Day}', 'MANG_DI', NULL, N'B1', 'MO', SYSUTCDATETIME())"));
        await ExecAsync(
            "INSERT INTO sales.DonHang (MaDon, NgayBanId, Loai, PhienSuDungId, NhanBanLucTao, TrangThai, TaoLuc) "
            + $"VALUES ('DH-2', '{Day}', 'TAI_BAN', 7, N'B1', 'MO', SYSUTCDATETIME())");
    }

    [SqlServerFact]
    public async Task The_order_aggregate_cannot_open_a_second_order_for_a_busy_session()
    {
        await using var db = CreateContext();
        db.Orders.Add(DonHang.Create("DH-1", OrderTypes.DineIn, Day, 7, null, null, Staff, Now).Value!);
        await db.SaveChangesAsync();
        await using var other = CreateContext();
        other.Orders.Add(DonHang.Create("DH-2", OrderTypes.DineIn, Day, 7, null, null, Staff, Now).Value!);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        Assert.Equal(2601, ((SqlException)error.InnerException!).Number);
    }

    // ---- helpers ----

    private static NewPortion Burger() => new(CatalogId.Of(1), CatalogId.Of(2), "Burger", "Single", 80000, Stations.Kitchen, null);

    private static Task<DonHang> Load(SalesDbContext db, long id) =>
        db.Orders.Include(x => x.Dots.OrderBy(d => d.SoDot)).ThenInclude(x => x.Portions.OrderBy(p => p.Id))
            .ThenInclude(x => x.Options).SingleAsync(x => x.Id == id);

    private static string OrderSql(string code, string type, string session, string status = "'MO'", string closed = "NULL") => $"""
        INSERT INTO sales.DonHang (MaDon, NgayBanId, Loai, PhienSuDungId, TrangThai, DongLuc, TaoLuc)
        VALUES ('{code}', '{Day}', '{type}', {session}, {status}, {closed}, DATEADD(minute, -10, SYSUTCDATETIME()));
        """;

    private static string BatchSql(
        long orderId, int number, string source, string grant, string creator, string status = "'CHO_XAC_NHAN'",
        bool decided = false, string reason = "NULL", Guid? key = null) => $"""
        INSERT INTO sales.DotGoiMon (DonHangId, SoDot, Nguon, QuyenTruyCapKhachId, NguoiTaoId, TrangThai, TaoLuc,
            XacNhanLuc, NguoiXacNhanId, LyDoTuChoi, KhoaYeuCau)
        VALUES ({orderId}, {number}, {source}, {grant}, {creator}, {status}, SYSUTCDATETIME(),
            {(decided ? "SYSUTCDATETIME()" : "NULL")}, {(decided ? $"'{Staff}'" : "NULL")}, {reason}, '{key ?? Guid.NewGuid()}');
        """;

    private static string PortionSql(long batchId, string station, long price, string status = "'CHO_XAC_NHAN'") => $"""
        INSERT INTO sales.ChiTietDon (DotGoiMonId, MonId, BienTheId, TenMonChot, TenBienTheChot, GiaMonChot, TramChot, TrangThai)
        VALUES ({batchId}, '{CatalogId.Of(1)}', '{CatalogId.Of(2)}', N'Dish', N'Variant', {price}, {station}, {status});
        """;

    private const string LastPortion = "(SELECT MAX(Id) FROM sales.ChiTietDon)";

    private static string OptionSql(string portionId, int optionId, string kind, long surcharge) => $"""
        INSERT INTO sales.TuyChonDaDat (ChiTietDonId, TuyChonId, NhomTuyChonId, TenNhomChot, LoaiNhomChot, TenTuyChonChot, GiaCongChot)
        VALUES ({portionId}, '{CatalogId.Of(optionId)}', '{CatalogId.Of(9)}', N'Group', {kind}, N'Option', {surcharge});
        """;

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
}
