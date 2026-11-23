using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Seating.Contracts;
using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Modules.Seating.Infrastructure;

/// <summary>
/// Read-only answer to "does this session exist and is it open", for Sales (HIE-3.1). It takes no lock and starts no
/// transaction: the result is a snapshot at read time, so "open" can be stale a moment later. A caller that must act on it
/// (creating an order for the session) takes the locks of ADR-01 itself (business day, then order/session) before it
/// relies on the answer. A closed session is never reopened, so "closed" is final.
/// </summary>
public sealed class SeatingSessionLookup(SeatingDbContext db) : ISessionLookup
{
    public async Task<SessionCheckResult> CheckOpenAsync(long sessionId, CancellationToken ct)
    {
        if (sessionId <= 0) return new SessionCheckResult(null, SeatingErrorCodes.SessionNotFound);

        // The table is read through the session's current table, so a transfer is reflected in the label.
        var row = await (
            from s in db.Sessions.AsNoTracking()
            join t in db.Tables.AsNoTracking() on s.BanHienTaiId equals t.Id
            where s.Id == sessionId
            select new { s.Id, s.NgayBanId, TableId = t.Id, t.Ten, s.MoLuc, s.DongLuc })
            .SingleOrDefaultAsync(ct);

        if (row is null) return new SessionCheckResult(null, SeatingErrorCodes.SessionNotFound);
        if (row.DongLuc is not null) return new SessionCheckResult(null, SeatingErrorCodes.SessionClosed);
        return new SessionCheckResult(
            new SessionInfo(row.Id, row.NgayBanId, row.TableId, row.Ten, SeatingRules.Utc(row.MoLuc)), null);
    }
}
