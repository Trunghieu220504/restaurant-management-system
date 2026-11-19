using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Transaction-scoped SQL application locks. Order: account administration first, then one user. Login and password change only
/// take the user lock, so the two never wait on each other in opposite order.
/// </summary>
internal static class IdentityLocks
{
    internal static Task<int> LockUserAsync(this IdentityDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.LockAsync("Restaurant.Identity.User." + userId.ToString("N"), cancellationToken);

    /// <summary>Serializes every account-management write, so "who may still manage accounts" is decided one change at a time.</summary>
    internal static Task<int> LockAccountAdministrationAsync(this IdentityDbContext db, CancellationToken cancellationToken) =>
        db.LockAsync("Restaurant.Identity.AccountAdministration", cancellationToken);

    private static Task<int> LockAsync(this IdentityDbContext db, string resource, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource},
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51001, 'Could not acquire Identity lock.', 1;
            """, cancellationToken);
}
