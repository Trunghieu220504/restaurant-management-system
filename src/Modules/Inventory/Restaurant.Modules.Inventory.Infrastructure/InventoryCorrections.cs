using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Inventory.Application;
using Restaurant.Modules.Inventory.Domain;
using Restaurant.Modules.Inventory.Infrastructure.Persistence;

namespace Restaurant.Modules.Inventory.Infrastructure;

public sealed class InventoryCorrections(InventoryDbContext db, InventoryService stock, IInventoryAudit audit) : IInventoryCorrections
{
    private static bool ReasonValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Length <= 512 && !reason.Any(char.IsControl);

    private static StockResult AdjustmentReplay(DieuChinhTon prior, StockAdjustment input, Guid actor) =>
        prior.NgayBanId == input.DayId && prior.NguyenLieuId == input.IngredientId && prior.NguoiDuyetId == actor &&
        prior.SoLuongChuan == input.Quantity && prior.GiaTriDieuChinh == input.Value && prior.NgayBanGocId == input.OriginalDayId &&
        prior.LyDo == input.Reason.Trim() ? new(prior.Id) : new(null, "inventory.idempotency.conflict");

    public async Task<StockResult> AdjustAsync(StockAdjustment input, Guid actor, Guid key, CancellationToken ct)
    {
        const decimal limit = 100000000000000000000m;
        if (key == Guid.Empty || actor == Guid.Empty || !ReasonValid(input.Reason) || input.OriginalDayId == Guid.Empty ||
            input.Quantity == 0 || input.Quantity <= -limit || input.Quantity >= limit || input.Value <= -limit || input.Value >= limit ||
            decimal.Round(input.Quantity, 8) != input.Quantity || decimal.Round(input.Value, 8) != input.Value ||
            (input.Value != 0 && Math.Sign(input.Quantity) != Math.Sign(input.Value))) return new(null, "inventory.input.invalid");
        var replay = await db.Set<DieuChinhTon>().AsNoTracking().SingleOrDefaultAsync(x => x.KhoaYeuCau == key, ct);
        if (replay != null) return AdjustmentReplay(replay, input, actor);
        return await stock.WriteAsync(input.DayId, async () =>
        {
            var prior = await db.Set<DieuChinhTon>().FromSqlInterpolated(
                $"SELECT * FROM inventory.DieuChinhTon WITH (UPDLOCK,HOLDLOCK) WHERE KhoaYeuCau={key}").SingleOrDefaultAsync(ct);
            if (prior != null) return AdjustmentReplay(prior, input, actor);
            if (!await db.Set<NguyenLieu>().AnyAsync(i => i.Id == input.IngredientId && !i.LuuTru && i.CheDo == "THEO_DOI_TON", ct))
                return new(null, "inventory.reference.not_found");
            await StockCounts.InitializeAsync(db, input.DayId, ct);
            var row = new DieuChinhTon
            {
                NgayBanId = input.DayId,
                NgayBanGocId = input.OriginalDayId,
                NguyenLieuId = input.IngredientId,
                SoLuongChuan = input.Quantity,
                GiaTriDieuChinh = input.Value,
                LyDo = input.Reason.Trim(),
                NguoiDuyetId = actor,
                TaoLuc = DateTimeOffset.UtcNow,
                KhoaYeuCau = key
            };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            await audit.AppendAsync(db.Database.CurrentTransaction!.GetDbTransaction(), actor, input.DayId,
                "stock.adjust", "DieuChinhTon", row.Id.ToString(), row.LyDo, ct);
            return new(row.Id);
        }, ct);
    }

    public async Task<StockResult> CorrectAsync(long id, PurchaseCorrection input, Guid actor, Guid key, CancellationToken ct)
    {
        if (input.Replacement == null || !ReasonValid(input.Reason) || key == Guid.Empty || actor == Guid.Empty)
            return new(null, "inventory.input.invalid");
        var day = input.Replacement.DayId;
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, input, actor })));
        var replay = await db.Set<PhieuNhap>().AsNoTracking().SingleOrDefaultAsync(r => r.KhoaYeuCau == key, ct);
        if (replay != null) return replay.DauVanTay == hash ? new(replay.Id) : new(null, "inventory.idempotency.conflict");
        return await stock.WriteAsync(day, async () =>
        {
            var original = await db.Set<PhieuNhap>().FromSqlInterpolated(
                $"SELECT * FROM inventory.PhieuNhap WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
            if (original == null || original.NgayBanId != day) return new(null, "inventory.reference.not_found");
            var prior = await db.Set<PhieuNhap>().SingleOrDefaultAsync(r => r.KhoaYeuCau == key, ct);
            if (prior != null) return prior.DauVanTay == hash ? new(prior.Id) : new(null, "inventory.idempotency.conflict");
            if (original.TrangThai == "HUY") return new(null, "inventory.purchase.invalid_transition");
            var result = await stock.CreateReceiptAsync(input.Replacement, actor, key, hash, original.TrangThai, ct);
            if (result.Error != null) return result;
            original.TrangThai = "HUY";
            await audit.AppendAsync(db.Database.CurrentTransaction!.GetDbTransaction(), actor, day, "purchase.replace",
                "PhieuNhap", $"{id}/{result.Id}", input.Reason.Trim(), ct);
            return result;
        }, ct);
    }
}
