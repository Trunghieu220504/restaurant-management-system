using Microsoft.EntityFrameworkCore;

using Restaurant.Modules.Seating.Infrastructure.Persistence;

namespace Restaurant.Modules.Seating.Infrastructure;

public static class SeatingCloseChecks
{
    public static async Task<(string Code, int Count)[]> ReadAsync(SeatingDbContext db, Guid day, CancellationToken ct) =>
        [
            ("salesday.open_sessions", await db.Sessions.CountAsync(s => s.NgayBanId == day && s.DongLuc == null, ct))
        ];
}
