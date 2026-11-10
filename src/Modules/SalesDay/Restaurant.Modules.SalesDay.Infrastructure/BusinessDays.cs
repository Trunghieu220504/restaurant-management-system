using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.SalesDay.Application;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;

namespace Restaurant.Modules.SalesDay.Infrastructure;

public sealed class BusinessDays(SalesDayDbContext db) : IBusinessDays
{
    private static BusinessDayResponse Map(BusinessDay d) => new(d.Id, d.BusinessDate, d.OpeningCash, d.Status, d.OpenedAt);
    public async Task<BusinessDayResponse?> CurrentAsync(CancellationToken ct)
    {
        var day = await db.Days.AsNoTracking().SingleOrDefaultAsync(x => x.ActiveSlot == 1, ct);
        return day is null ? null : Map(day);
    }
    public async Task<OpenDayResult> OpenAsync(OpenDayRequest request, Guid actor, Guid key, CancellationToken ct)
    {
        if (request.OpeningCash < 0 || request.BusinessDate == default || actor == Guid.Empty || key == Guid.Empty)
            return new(null, "salesday.input.invalid");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Global gate: dates must not allow competing openings to bypass the lock.
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource=N'restaurant:salesday:open', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Sales day lock timeout', 1;", ct);
        var old = await db.Days.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (old is not null)
            return old.BusinessDate == request.BusinessDate && old.OpeningCash == request.OpeningCash && old.OpenedBy == actor
                ? new(Map(old), null, true) : new(null, "salesday.idempotency.conflict");
        if (await db.Days.AnyAsync(x => x.ActiveSlot == 1, ct)) return new(null, "salesday.previous_not_closed");
        if (await db.Days.AnyAsync(x => x.BusinessDate >= request.BusinessDate, ct)) return new(null, "salesday.date.invalid");
        var day = new BusinessDay { Id = Guid.NewGuid(), BusinessDate = request.BusinessDate, OpeningCash = request.OpeningCash, OpenedBy = actor, OpenedAt = DateTimeOffset.UtcNow, RequestKey = key };
        db.Days.Add(day);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(Map(day), null);
    }
}
