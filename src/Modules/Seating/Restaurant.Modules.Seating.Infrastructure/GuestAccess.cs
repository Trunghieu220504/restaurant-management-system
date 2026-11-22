using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Application;
using Restaurant.Modules.Seating.Domain;
using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Modules.Seating.Infrastructure;

internal sealed class GuestAccess(SeatingDbContext db, TimeProvider clock) : IGuestAccess
{
    private static byte[]? Hash(string? text)
    {
        if (text?.Length != 43) return null;
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(text);
            return bytes.Length == 32 && WebEncoders.Base64UrlEncode(bytes) == text ? SHA256.HashData(bytes) : null;
        }
        catch (FormatException) { return null; }
    }

    private Task<PhienSuDung?> LockSession(long id, CancellationToken ct) =>
        db.Sessions.FromSqlInterpolated($"SELECT * FROM seating.PhienSuDung WITH (UPDLOCK) WHERE Id={id}")
            .SingleOrDefaultAsync(ct);

    public async Task<SeatingResult<GuestInvite>> InviteAsync(long session, Guid actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await LockSession(session, ct);
        if (row is null || row.DongLuc is not null) return SeatingResult<GuestInvite>.Fail(SeatingErrors.SessionClosed);
        var locator = await db.Tables.Where(t => t.Id == row.BanHienTaiId).Select(t => t.MaQrCongKhai).SingleAsync(ct);
        var bytes = RandomNumberGenerator.GetBytes(32);
        var hash = SHA256.HashData(bytes);
        var now = clock.GetUtcNow();
        var expiry = now.AddMinutes(10);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO seating.MaThamGiaPhien (MaHash, PhienSuDungId, NguoiCapId, TaoLuc, HetHanLuc)
            VALUES ({hash},{session},{actor},{now.UtcDateTime},{expiry.UtcDateTime})
            """, ct);
        await tx.CommitAsync(ct);
        return SeatingResult<GuestInvite>.Ok(new(locator, WebEncoders.Base64UrlEncode(bytes), expiry));
    }

    private sealed record Invitation(long PhienSuDungId, DateTime HetHanLuc, DateTime? DaDungLuc, DateTime? ThuHoiLuc);

    public async Task<SeatingResult<GuestToken>> ExchangeAsync(GuestExchange request, CancellationToken ct)
    {
        const string invalid = "qr.code.invalid";
        var hash = Hash(request.JoinCode);
        if (hash is null || string.IsNullOrWhiteSpace(request.TableCode) || request.TableCode.Length > 100)
            return SeatingResult<GuestToken>.Fail(invalid);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var invite = await db.Database.SqlQuery<Invitation>($"""
            SELECT PhienSuDungId,HetHanLuc,DaDungLuc,ThuHoiLuc FROM seating.MaThamGiaPhien WHERE MaHash={hash}
            """).SingleOrDefaultAsync(ct);
        if (invite is null) return SeatingResult<GuestToken>.Fail(invalid);
        var session = await LockSession(invite.PhienSuDungId, ct);
        if (session is null || session.DongLuc is not null || invite.ThuHoiLuc is not null) return SeatingResult<GuestToken>.Fail(invalid);
        var tableCode = await db.Tables.Where(t => t.Id == session.BanHienTaiId).Select(t => t.MaQrCongKhai).SingleAsync(ct);
        if (request.TableCode != tableCode) return SeatingResult<GuestToken>.Fail(invalid);
        var now = clock.GetUtcNow();
        if (invite.HetHanLuc <= now.UtcDateTime) return SeatingResult<GuestToken>.Fail("qr.code.expired");
        var used = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE seating.MaThamGiaPhien SET DaDungLuc={now.UtcDateTime}
            WHERE MaHash={hash} AND DaDungLuc IS NULL AND ThuHoiLuc IS NULL
            """,ct);
        if (used != 1) return SeatingResult<GuestToken>.Fail("qr.code.used");
        var bytes = RandomNumberGenerator.GetBytes(32);
        var expiry = now.AddHours(4);
        db.Grants.Add(new QuyenTruyCapKhach { Id = Guid.NewGuid(), PhienSuDungId=session.Id, TokenHash=SHA256.HashData(bytes),
            TaoLuc=now.UtcDateTime, HetHanLuc=expiry.UtcDateTime });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return SeatingResult<GuestToken>.Ok(new(WebEncoders.Base64UrlEncode(bytes), expiry));
    }

    public async Task<SeatingResult<GuestSession>> CheckAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        if (hash is null) return SeatingResult<GuestSession>.Fail("qr.token.invalid");
        var row = await (from g in db.Grants join s in db.Sessions on g.PhienSuDungId equals s.Id
                         join t in db.Tables on s.BanHienTaiId equals t.Id where g.TokenHash == hash
                         select new {g.HetHanLuc,g.ThuHoiLuc,s.DongLuc,t.Ten}).SingleOrDefaultAsync(ct);
        if (row is null) return SeatingResult<GuestSession>.Fail("qr.token.invalid");
        if (row.ThuHoiLuc is not null || row.DongLuc is not null) return SeatingResult<GuestSession>.Fail("qr.token.revoked");
        if (row.HetHanLuc <= clock.GetUtcNow().UtcDateTime) return SeatingResult<GuestSession>.Fail("qr.token.expired");
        return SeatingResult<GuestSession>.Ok(new(row.Ten, DateTime.SpecifyKind(row.HetHanLuc, DateTimeKind.Utc)));
    }
}
