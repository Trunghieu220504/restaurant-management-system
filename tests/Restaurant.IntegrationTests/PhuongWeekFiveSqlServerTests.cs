using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Restaurant.Api.Modules;
using Restaurant.Modules.Billing.Domain;
using Restaurant.Modules.Billing.Infrastructure.Persistence;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;
using Restaurant.Modules.Seating.Infrastructure.Persistence;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class PhuongWeekFiveSqlServerTests : DayLedgerSqlFixture
{
    [SqlServerFact]
    public async Task An_archived_ingredient_with_carried_stock_remains_countable()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var (item, unit) = await MaterialAsync(p, "ARCHIVED");
        await PurchaseAsync(p, item, unit, 10, 5);
        await ApproveAsync(p, item, 4);
        await BeginAsync(p);
        var closing = await p.GetRequiredService<IDaySettlement>().FinalizeAsync(day, new(500, null, false, null), actor, Guid.NewGuid(), default);
        Assert.Null(closing.Error);
        var inventory = p.GetRequiredService<InventoryDbContext>();
        // Simulate legacy archived inventory; current API correctly refuses this structural change.
        await inventory.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.NguyenLieu SET LuuTru=1 WHERE Id={item}");
        var next = await p.GetRequiredService<IBusinessDays>().OpenAsync(new(new(2026, 12, 11), 500), actor, Guid.NewGuid(), default);
        Assert.Null(next.Error);
        var counts = p.GetRequiredService<IStockCounts>();
        var row = Assert.Single(await counts.ListAsync(next.Day!.Id, default));
        Assert.Equal(item, row.IngredientId);
        Assert.Equal(4, row.OpeningQuantity);
        Assert.Null((await counts.SaveAsync(next.Day.Id, item, 3, row.Version, false, actor, default)).Error);
        var edited = Assert.Single(await counts.ListAsync(next.Day.Id, default));
        Assert.Null((await counts.SaveAsync(next.Day.Id, item, null, edited.Version, true, actor, default)).Error);
        Assert.Equal(3, Assert.Single(await counts.ListAsync(next.Day.Id, default)).Approved);
    }

    [SqlServerFact]
    public async Task Zero_quantity_with_residual_value_blocks_freeze_and_new_draft_invalidates_approval()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var (item, _) = await MaterialAsync(p, "SUGAR");
        var inventory = p.GetRequiredService<InventoryDbContext>();
        await inventory.Set<KiemKeTon>().ExecuteUpdateAsync(q => q.SetProperty(k => k.GiaTriTonDau, 1m));
        await ApproveAsync(p, item, 0);
        var close = await p.GetRequiredService<IDayClosing>().ChangeAsync(day, false, actor, Guid.NewGuid(), default);
        Assert.Contains(new CloseBlocker("inventory.count.invalid_valuation", 1), close.Checklist!.Blockers);
        var counts = p.GetRequiredService<IStockCounts>();
        var version = (await counts.ListAsync(day, default)).Single().Version;
        Assert.Null((await counts.SaveAsync(day, item, 0, version, false, actor, default)).Error);
        Assert.Null((await counts.ListAsync(day, default)).Single().Approved);
    }

    [SqlServerFact]
    public async Task Cost_rounds_once_across_units_and_is_carried_to_the_next_day()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var kg = await MaterialAsync(p, "KG", "KHOI_LUONG");
        var bottle = await MaterialAsync(p, "BOTTLE");
        foreach (var line in new[] { kg, bottle })
        {
            var packed = (await p.GetRequiredService<IInventory>().UnitAsync(new("P" + line.Unit, "Pack", "DEM"), default)).Id!.Value;
            Assert.Null((await p.GetRequiredService<IInventory>().ConversionAsync(line.Ingredient, packed, new(day, 3), default)).Error);
            await PurchaseAsync(p, line.Ingredient, packed, 1, 100);
            await ApproveAsync(p, line.Ingredient, 2);
        }
        var packaging = await MaterialAsync(p, "BOX", expense: true);
        await PurchaseAsync(p, packaging.Ingredient, packaging.Unit, 1, 50, drawer: true);
        var service = p.GetRequiredService<IDaySettlement>();
        var preview = (await service.PreviewAsync(day, default))!;
        Assert.Equal(2, preview.Ingredients.Count);
        Assert.Equal(2, preview.Ingredients.Select(l => l.UnitId).Distinct().Count());
        Assert.All(preview.Ingredients, l => Assert.Equal(1, l.Usage));
        Assert.Equal(117, preview.Cost);
        Assert.Equal(-117, preview.GrossProfit);
        Assert.Equal(0.33333333m, preview.RoundingDifference);
        await BeginAsync(p);
        var request = new FinalizeDay(450, null, true, "End of shift was missed");
        var key = Guid.NewGuid();
        var result = await service.FinalizeAsync(day, request, actor, key, default);
        Assert.Null(result.Error);
        Assert.Equal(450, result.Snapshot!.ExpectedCash);
        Assert.True(result.Snapshot.LateClose);
        Assert.Equal(result.Snapshot.ClosedAt, (await service.FinalizeAsync(day, request, actor, key, default)).Snapshot!.ClosedAt);
        Assert.Equal("salesday.idempotency.conflict", (await service.FinalizeAsync(day, request with { CountedCash = 451 }, actor, key, default)).Error);
        var next = (await p.GetRequiredService<IBusinessDays>().OpenAsync(new(new(2026, 12, 11), 450), actor, Guid.NewGuid(), default)).Day!;
        var opening = await p.GetRequiredService<IStockCounts>().ListAsync(next.Id, default);
        Assert.Equal(2, opening.Count);
        Assert.All(opening, line => { Assert.Equal(2, line.OpeningQuantity); Assert.Equal(66.66666667m, line.OpeningValue); Assert.Null(line.Draft); });
        Assert.Equal(117, (await service.SnapshotAsync(day, default))!.Cost);
    }

    [SqlServerFact]
    public async Task Internal_stock_precision_cannot_round_half_a_dong_up_early()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var item = await MaterialAsync(p, "MILK", "THE_TICH");
        var stock = p.GetRequiredService<IInventory>();
        var pack = (await stock.UnitAsync(new("PACK", "Pack", "DEM"), default)).Id!.Value;
        Assert.Null((await stock.ConversionAsync(item.Ingredient, pack, new(day, 5), default)).Error);
        await PurchaseAsync(p, item.Ingredient, pack, 1, 1);
        await ApproveAsync(p, item.Ingredient, 2.50000001m);
        var preview = (await p.GetRequiredService<IDaySettlement>().PreviewAsync(day, default))!;
        Assert.Equal(0.499999998m, Assert.Single(preview.Ingredients).Cost);
        Assert.Equal(0m, preview.Cost);
    }

    [SqlServerFact]
    public async Task Cash_reconciliation_separates_revenue_from_execution_day_cash_and_refunds()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var db = p.GetRequiredService<BillingDbContext>();
        var now = DateTime.UtcNow;
        var invoice = new HoaDon { DonHangId = 17, NgayBanLapId = day, MaHoaDon = "HD17", TaoLuc = now };
        db.Add(invoice);
        await db.SaveChangesAsync();
        db.Add(new DongHoaDon { HoaDonId = invoice.Id, ChiTietDonId = 1, TenMonChot = "Tea", ThanhTienGoc = 300 });
        db.Add(new DieuChinhNghiaVu
        {
            HoaDonId = invoice.Id,
            NgayBanId = day,
            SoTien = -30,
            LyDo = "Incident",
            KhoaYeuCau = Guid.NewGuid(),
            NguoiDuyetId = actor,
            TaoLuc = now
        });
        db.Add(new MienThu
        {
            HoaDonId = invoice.Id,
            NgayBanId = day,
            SoTien = 20,
            LyDo = "Waiver",
            KhoaYeuCau = Guid.NewGuid(),
            NguoiDuyetId = actor,
            TaoLuc = now
        });
        var cash = new ThanhToan
        {
            HoaDonId = invoice.Id,
            NgayBanId = day,
            PhuongThuc = "TIEN_MAT",
            TrangThai = "THANH_CONG",
            SoTien = 100,
            TienKhachDua = 150,
            TienThua = 50,
            KhoaYeuCau = Guid.NewGuid(),
            TaoLuc = now
        };
        db.Add(cash);
        db.Add(new ThanhToan
        {
            HoaDonId = invoice.Id,
            NgayBanId = day,
            PhuongThuc = "CHUYEN_KHOAN",
            TrangThai = "THANH_CONG",
            SoTien = 150,
            KhoaYeuCau = Guid.NewGuid(),
            TaoLuc = now
        });
        await db.SaveChangesAsync();
        db.Add(new HoanTien
        {
            ThanhToanId = cash.Id,
            NgayBanId = day,
            PhuongThuc = "TIEN_MAT",
            TrangThai = "THANH_CONG",
            SoTien = 10,
            LyDo = "Refund",
            KhoaYeuCau = Guid.NewGuid(),
            TaoLuc = now
        });
        await db.SaveChangesAsync();
        var box = await MaterialAsync(p, "BOX", expense: true);
        await PurchaseAsync(p, box.Ingredient, box.Unit, 2, 5, drawer: true);
        await BeginAsync(p);
        var service = p.GetRequiredService<IDaySettlement>();
        Assert.Equal("salesday.cash.reason_required", (await service.FinalizeAsync(day, new(575, null, false, null), actor, Guid.NewGuid(), default)).Error);
        var result = await service.FinalizeAsync(day, new(575, "Drawer short by 5", false, null), actor, Guid.NewGuid(), default);
        Assert.Null(result.Error);
        var summary = result.Snapshot!;
        Assert.Equal(580, summary.ExpectedCash);
        Assert.Equal(575, summary.CountedCash);
        Assert.Equal(250, summary.NetRevenue);
        Assert.Equal(240, summary.GrossProfit);
        Assert.Equal(90, summary.Methods.Single(m => m.Method == "TIEN_MAT").Net);
        Assert.Equal(150, summary.Methods.Single(m => m.Method == "CHUYEN_KHOAN").Net);
        Assert.Equal(summary.DayId, (await service.HistoryAsync(0, 10, default)).Single().DayId);
    }

    [SqlServerFact]
    public async Task Count_entry_and_approval_are_enforced_by_role_through_http()
    {
        await using var scope = services!.CreateAsyncScope();
        var (item, _) = await MaterialAsync(scope.ServiceProvider, "ACCESS");
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Restaurant"] = config.GetConnectionString("Restaurant") })));
        await using var web = factory.Services.CreateAsyncScope();
        await web.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        var users = web.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        using var client = factory.CreateClient();
        var url = $"/api/inventory/days/{day}/counts";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        foreach (var role in new[] { "Staff", "Kitchen", "Bar", "Manager" })
        {
            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = "count-" + role, Email = $"count-{role}@example.invalid" };
            var password = "Aa1!" + Guid.NewGuid().ToString("N");
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            client.DefaultRequestHeaders.Authorization = null;
            using var login = await client.PostAsJsonAsync("/api/identity/login", new { userName = user.UserName, password });
            login.EnsureSuccessStatusCode();
            using var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("accessToken").GetString());
            var row = Assert.Single((await client.GetFromJsonAsync<List<StockCountView>>(url))!);
            Assert.Equal(role == "Manager", row.OpeningValue.HasValue);
            (await client.PutAsJsonAsync($"{url}/{item}", new DraftCount(0, row.Version))).EnsureSuccessStatusCode();
            var revised = Assert.Single((await client.GetFromJsonAsync<List<StockCountView>>(url))!);
            using var approval = await client.PostAsJsonAsync($"{url}/{item}/approve", new ApproveCount(revised.Version!));
            Assert.Equal(role == "Manager" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, approval.StatusCode);
            foreach (var reportPath in new[] { "daily?date=2026-12-10", "range?from=2026-12-10&to=2026-12-11",
                "stock?from=2026-12-10&to=2026-12-11", "items-sold?from=2026-12-10&to=2026-12-11" })
            {
                using var report = await client.GetAsync("/api/reporting/" + reportPath);
                Assert.Equal(role == "Manager" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, report.StatusCode);
            }
            using var history = await client.GetAsync("/api/salesday/history");
            Assert.Equal(role == "Manager" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, history.StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Signed_stock_adjustments_change_both_quantity_and_value_in_the_average()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var item = await MaterialAsync(p, "TEA");
        await PurchaseAsync(p, item.Ingredient, item.Unit, 10, 5);
        var inventory = p.GetRequiredService<InventoryDbContext>();
        inventory.Add(new DieuChinhTon
        {
            NgayBanId = day,
            NguyenLieuId = item.Ingredient,
            SoLuongChuan = -2,
            GiaTriDieuChinh = -10,
            LyDo = "Correction",
            NguoiDuyetId = actor,
            TaoLuc = DateTimeOffset.UtcNow,
            KhoaYeuCau = Guid.NewGuid()
        });
        await inventory.SaveChangesAsync();
        await ApproveAsync(p, item.Ingredient, 5);
        var view = await p.GetRequiredService<IDaySettlement>().PreviewAsync(day, default);
        var line = Assert.Single(view!.Ingredients);
        Assert.Equal(5, line.Average);
        Assert.Equal(3, line.Usage);
        Assert.Equal(25, line.ClosingValue);
        Assert.Equal(15, line.Cost);
    }

    [SqlServerFact]
    public async Task Day_opened_before_rollout_can_submit_a_missing_count_using_its_previous_closing_balance()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var material = await MaterialAsync(p, "LEGACY");
        var db = p.GetRequiredService<InventoryDbContext>();
        await db.Set<KiemKeTon>().Where(k => k.NgayBanId == day).ExecuteDeleteAsync();
        db.Add(new KiemKeTon
        {
            NgayBanId = Guid.NewGuid(),
            NguyenLieuId = material.Ingredient,
            CheDoChot = "THEO_DOI_TON",
            ChotLuc = DateTimeOffset.UtcNow.AddDays(-1),
            SoDemDuyet = 2,
            GiaTriTonCuoi = 13.125m
        });
        await db.SaveChangesAsync();
        var count = Assert.Single(await p.GetRequiredService<IStockCounts>().ListAsync(day, default));
        Assert.Null(count.Version);
        Assert.Equal(13.125m, count.OpeningValue);
        var service = p.GetRequiredService<IStockCounts>();
        Assert.Null((await service.SaveAsync(day, material.Ingredient, 1, null, false, actor, default)).Error);
        var draft = Assert.Single(await service.ListAsync(day, default));
        Assert.Equal(2, draft.OpeningQuantity);
        Assert.Null((await service.SaveAsync(day, material.Ingredient, null, draft.Version, true, actor, default)).Error);
        await BeginAsync(p);
        Assert.Equal("salesday.day.not_open", (await service.SaveAsync(day, material.Ingredient, 2, draft.Version, false, actor, default)).Error);
        var final = await p.GetRequiredService<IDaySettlement>().FinalizeAsync(day, new(500, null, false, null), actor, Guid.NewGuid(), default);
        Assert.Null(final.Error);
        var frozen = Assert.Single(await service.ListAsync(day, default)).FrozenCost!;
        Assert.Equal(6.5625m, frozen.ClosingValue);
    }

    [SqlServerFact]
    public async Task Two_managers_commit_one_snapshot_and_one_outbox_event()
    {
        await using var scope = services!.CreateAsyncScope();
        await BeginAsync(scope.ServiceProvider);
        async Task<SettlementResult> Close()
        {
            await using var other = services!.CreateAsyncScope();
            return await other.ServiceProvider.GetRequiredService<IDaySettlement>().FinalizeAsync(day, new(500, null, false, null), Guid.NewGuid(), Guid.NewGuid(), default);
        }
        var results = await Task.WhenAll(Close(), Close());
        Assert.Single(results, x => x.Snapshot != null);
        Assert.Single(results, x => x.Error == "salesday.day.already_closed");
        var db = scope.ServiceProvider.GetRequiredService<SalesDayDbContext>();
        Assert.Equal(1, await db.Set<DaySnapshot>().CountAsync());
        Assert.Equal(2, await db.Set<DayMethodSnapshot>().CountAsync());
        Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM salesday.Outbox").SingleAsync());
    }

    [SqlServerFact]
    public async Task Failure_after_stock_write_rolls_back_stock_snapshot_event_and_day_then_allows_retry()
    {
        await using var scope = services!.CreateAsyncScope();
        var p = scope.ServiceProvider;
        var (item, _) = await MaterialAsync(p, "TEST");
        await ApproveAsync(p, item, 0);
        await BeginAsync(p);
        var db = p.GetRequiredService<SalesDayDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER salesday.TestRejectClose ON salesday.TongHopNgay AFTER INSERT AS
            BEGIN THROW 51088, 'Injected snapshot failure', 1; END
            """);
        var service = p.GetRequiredService<IDaySettlement>();
        var key = Guid.NewGuid();
        var request = new FinalizeDay(500, null, false, null);
        await Assert.ThrowsAsync<DbUpdateException>(() => service.FinalizeAsync(day, request, actor, key, default));
        Assert.Equal("CHO_CHOT", (await db.Days.AsNoTracking().SingleAsync()).Status);
        Assert.Null((await p.GetRequiredService<InventoryDbContext>().Set<KiemKeTon>().AsNoTracking().SingleAsync()).ChotLuc);
        Assert.Empty(await db.Set<DaySnapshot>().ToListAsync());
        Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM salesday.Outbox").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER salesday.TestRejectClose");
        Assert.Null((await service.FinalizeAsync(day, request, actor, key, default)).Error);
    }
}
