using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;

namespace Restaurant.Modules.Inventory.Infrastructure;

public sealed class InventoryService(InventoryDbContext db, IInventoryDayGate days) : IInventory
{
    private const string Invalid = "inventory.input.invalid";
    private const string Missing = "inventory.reference.not_found";
    private static StockResult Fail(string error) => new(null, error);
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    private static bool Fits(decimal value, decimal max, int scale) => value >= 0 && value <= max && decimal.Round(value, scale) == value;

    public async Task<StockCatalog> CatalogAsync(CancellationToken ct) => new(
        await db.Set<DonVi>().AsNoTracking().OrderBy(x => x.Ma).ToListAsync(ct),
        await db.Set<NguyenLieu>().AsNoTracking().OrderBy(x => x.Ma).ToListAsync(ct),
        await (from q in db.Set<QuyDoi>().AsNoTracking()
               join i in db.Set<NguyenLieu>() on q.NguyenLieuId equals i.Id
               where q.DonViChuanId == i.DonViChuanId
               orderby q.NguyenLieuId, q.DonViNhapId
               select q).ToListAsync(ct),
        await db.Set<NhaCungCap>().AsNoTracking().OrderBy(x => x.Ten).ToListAsync(ct));

    private static readonly System.Linq.Expressions.Expression<Func<PhieuNhap, PurchaseHeader>> Header =
        x => new(x.Id, x.NgayBanId, x.TrangThai, x.TraBangTienKet, x.NhaCungCapId, x.GhiChu, x.TaoLuc, x.NguoiLapId);

    public async Task<IReadOnlyList<PurchaseHeader>> PurchasesAsync(Guid dayId, CancellationToken ct) =>
        await db.Set<PhieuNhap>().AsNoTracking().Where(x => x.NgayBanId == dayId).OrderBy(x => x.Id)
            .Select(Header).ToListAsync(ct);

    public async Task<PurchaseView?> PurchaseAsync(long id, CancellationToken ct)
    {
        var header = await db.Set<PhieuNhap>().AsNoTracking().Where(x => x.Id == id).Select(Header).SingleOrDefaultAsync(ct);
        if (header is null) return null;
        var lines = await db.Set<ChiTietPhieuNhap>().AsNoTracking().Where(x => x.PhieuNhapId == id).OrderBy(x => x.Id).ToListAsync(ct);
        return new(header, lines, lines.Sum(x => x.ThanhTien));
    }

    private async Task<StockResult> WriteAsync(Guid? day, Func<Task<StockResult>> write, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (day.HasValue && (day == Guid.Empty || !await days.RequireOpenAsync(tx.GetDbTransaction(), day.Value, ct)))
                return Fail("salesday.day.not_open");
            var result = await write();
            if (result.Error is null)
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return result;
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 2601 or 2627 or 1205 } || ex is DbUpdateConcurrencyException or TimeoutException)
        { return Fail("inventory.write.conflict"); }
        finally { db.ChangeTracker.Clear(); }
    }

    public Task<StockResult> UnitAsync(UnitInput input, CancellationToken ct) => WriteAsync(null, async () =>
    {
        if (!Text(input.Code, 32) || !Text(input.Name, 150) || input.Dimension is not ("KHOI_LUONG" or "THE_TICH" or "DEM")) return Fail(Invalid);
        var unit = new DonVi { Ma = input.Code.Trim(), Ten = input.Name.Trim(), LoaiDaiLuong = input.Dimension };
        db.Add(unit);
        await db.SaveChangesAsync(ct);
        return new(unit.Id);
    }, ct);

    public Task<StockResult> SupplierAsync(long? id, SupplierInput input, CancellationToken ct) => WriteAsync(null, async () =>
    {
        if (!Text(input.Name, 150) || input.Phone?.Length > 32 || input.Address?.Length > 300) return Fail(Invalid);
        var supplier = id.HasValue ? await db.Set<NhaCungCap>().FindAsync([id.Value], ct) : db.Add(new NhaCungCap()).Entity;
        if (supplier is null) return Fail(Missing);
        supplier.Ten = input.Name.Trim();
        supplier.DienThoai = input.Phone;
        supplier.DiaChi = input.Address;
        supplier.HoatDong = input.Active;
        await db.SaveChangesAsync(ct);
        return new(supplier.Id);
    }, ct);

    private Task<NguyenLieu?> IngredientForWrite(long id, CancellationToken ct) => db.Set<NguyenLieu>()
        .FromSqlInterpolated($"SELECT * FROM inventory.NguyenLieu WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);

    private async Task<bool> HasStockOrMovements(long id, Guid day, CancellationToken ct)
    {
        var counts = db.Set<KiemKeTon>().Where(x => x.NguyenLieuId == id);
        var last = await counts.Where(x => x.ChotLuc != null).OrderByDescending(x => x.ChotLuc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (last?.SoDemDuyet > 0 || last?.GiaTriTonCuoi > 0) return true;
        if (await counts.AnyAsync(x => x.NgayBanId == day && (x.TonDau > 0 || x.GiaTriTonDau > 0 || x.SoDemNhap != null || x.SoDemDuyet != null), ct)) return true;
        return await (from line in db.Set<ChiTietPhieuNhap>()
                      join receipt in db.Set<PhieuNhap>() on line.PhieuNhapId equals receipt.Id
                      where line.NguyenLieuId == id && receipt.TrangThai != "HUY" &&
                      (receipt.NgayBanId == day || (line.CheDoChot == "THEO_DOI_TON" && receipt.TrangThai == "HIEU_LUC" &&
                        !counts.Any(c => c.NgayBanId == receipt.NgayBanId && c.ChotLuc != null)))
                      select line.Id).AnyAsync(ct);
    }

    public Task<StockResult> IngredientAsync(long? id, IngredientInput input, CancellationToken ct) => WriteAsync(input.DayId, async () =>
    {
        if (!Text(input.Code, 32) || !Text(input.Name, 150) || input.Mode is not ("THEO_DOI_TON" or "TINH_KHI_MUA")) return Fail(Invalid);
        if (!await db.Set<DonVi>().AnyAsync(x => x.Id == input.BaseUnitId, ct)) return Fail(Missing);
        var item = id.HasValue ? await IngredientForWrite(id.Value, ct) : db.Add(new NguyenLieu()).Entity;
        if (item is null) return Fail(Missing);
        if (id.HasValue && (item.DonViChuanId != input.BaseUnitId || item.CheDo != input.Mode || (!item.LuuTru && input.Archived)) &&
            await HasStockOrMovements(item.Id, input.DayId, ct)) return Fail("inventory.ingredient.has_stock_or_movements");
        item.Ma = input.Code.Trim();
        item.Ten = input.Name.Trim();
        item.DonViChuanId = input.BaseUnitId;
        item.CheDo = input.Mode;
        item.LuuTru = input.Archived;
        await db.SaveChangesAsync(ct);
        var unit = await db.Set<QuyDoi>().FindAsync([item.Id, input.BaseUnitId], ct);
        if (unit is null) db.Add(unit = new QuyDoi { NguyenLieuId = item.Id, DonViNhapId = input.BaseUnitId });
        unit.DonViChuanId = input.BaseUnitId;
        unit.HeSoVeChuan = 1;
        if (!item.LuuTru && item.CheDo == "THEO_DOI_TON" && !await db.Set<KiemKeTon>().AnyAsync(x => x.NgayBanId == input.DayId && x.NguyenLieuId == item.Id, ct))
        {
            var last = await db.Set<KiemKeTon>().Where(x => x.NguyenLieuId == item.Id && x.ChotLuc != null).OrderByDescending(x => x.ChotLuc).FirstOrDefaultAsync(ct);
            db.Add(new KiemKeTon
            {
                NgayBanId = input.DayId,
                NguyenLieuId = item.Id,
                CheDoChot = item.CheDo,
                TonDau = last?.SoDemDuyet ?? 0,
                GiaTriTonDau = last?.GiaTriTonCuoi ?? 0
            });
        }
        return new(item.Id);
    }, ct);

    public Task<StockResult> ConversionAsync(long ingredient, long unit, ConversionInput input, CancellationToken ct) => WriteAsync(input.DayId, async () =>
    {
        var item = await IngredientForWrite(ingredient, ct);
        if (item is null || !await db.Set<DonVi>().AnyAsync(x => x.Id == unit, ct)) return Fail(Missing);
        if (item.LuuTru || input.Factor <= 0 || !Fits(input.Factor, 999999999999.999999m, 6) || unit == item.DonViChuanId && input.Factor != 1) return Fail(Invalid);
        var row = await db.Set<QuyDoi>().FindAsync([ingredient, unit], ct);
        if (row is null) db.Add(row = new QuyDoi { NguyenLieuId = ingredient, DonViNhapId = unit });
        row.DonViChuanId = item.DonViChuanId;
        row.HeSoVeChuan = input.Factor;
        return new(ingredient);
    }, ct);

    public Task<StockResult> DraftAsync(PurchaseInput input, Guid actor, Guid key, CancellationToken ct) => WriteAsync(input.DayId, async () =>
    {
        if (key == Guid.Empty || actor == Guid.Empty || input.Lines is not { Count: > 0 } || input.Note?.Length > 2000) return Fail(Invalid);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input, actor })));
        var old = await db.Set<PhieuNhap>().FromSqlInterpolated($"SELECT * FROM inventory.PhieuNhap WITH (UPDLOCK,HOLDLOCK) WHERE KhoaYeuCau={key}").SingleOrDefaultAsync(ct);
        if (old is not null) return old.DauVanTay == hash ? new(old.Id) : Fail("inventory.idempotency.conflict");
        if (input.SupplierId.HasValue && !await db.Set<NhaCungCap>().AnyAsync(x => x.Id == input.SupplierId && x.HoatDong, ct)) return Fail(Missing);
        var lines = new List<ChiTietPhieuNhap>();
        foreach (var line in input.Lines)
        {
            if (line is null || line.Quantity <= 0 || !Fits(line.Quantity, 99999999999999.9999m, 4) || !Fits(line.UnitPrice, 999999999999999999m, 0)) return Fail(Invalid);
            var material = await db.Set<NguyenLieu>().FindAsync([line.IngredientId], ct);
            var conversion = await db.Set<QuyDoi>().FindAsync([line.IngredientId, line.UnitId], ct);
            if (material is null || material.LuuTru || conversion is null || conversion.DonViChuanId != material.DonViChuanId) return Fail(Missing);
            decimal quantity, amount;
            try
            {
                quantity = decimal.Round(line.Quantity * conversion.HeSoVeChuan, 8, MidpointRounding.AwayFromZero);
                amount = decimal.Round(line.Quantity * line.UnitPrice, 0, MidpointRounding.AwayFromZero);
            }
            catch (OverflowException) { return Fail(Invalid); }
            if (quantity <= 0 || quantity >= 100000000000000000000m || amount >= 1000000000000000000m) return Fail(Invalid);
            lines.Add(new ChiTietPhieuNhap
            {
                NguyenLieuId = material.Id,
                DonViId = line.UnitId,
                DonViChuanChotId = material.DonViChuanId,
                SoLuong = line.Quantity,
                DonGiaNhap = line.UnitPrice,
                HeSoChot = conversion.HeSoVeChuan,
                CheDoChot = material.CheDo,
                SoLuongChuan = quantity,
                ThanhTien = amount
            });
        }
        var header = new PhieuNhap
        {
            NgayBanId = input.DayId,
            NhaCungCapId = input.SupplierId,
            TrangThai = "NHAP",
            TraBangTienKet = input.CashDrawer,
            GhiChu = input.Note,
            TaoLuc = DateTimeOffset.UtcNow,
            NguoiLapId = actor,
            KhoaYeuCau = key,
            DauVanTay = hash
        };
        db.Add(header);
        await db.SaveChangesAsync(ct);
        foreach (var line in lines)
        {
            line.PhieuNhapId = header.Id;
            db.Add(line);
        }
        return new(header.Id);
    }, ct);

    public Task<StockResult> TransitionAsync(long id, Guid day, bool post, CancellationToken ct) => WriteAsync(day, async () =>
    {
        var row = await db.Set<PhieuNhap>().FromSqlInterpolated($"SELECT * FROM inventory.PhieuNhap WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
        if (row is null || row.NgayBanId != day) return Fail(Missing);
        var status = post ? "HIEU_LUC" : "HUY";
        if (row.TrangThai == status) return new(id);
        if (row.TrangThai != "NHAP") return Fail("inventory.purchase.invalid_transition");
        row.TrangThai = status;
        return new(id);
    }, ct);
}
