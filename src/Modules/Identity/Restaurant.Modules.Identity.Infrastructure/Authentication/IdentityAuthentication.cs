using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;

namespace Restaurant.Modules.Identity.Infrastructure.Authentication;

public sealed class IdentityAuthentication(
    IdentityDbContext db,
    UserManager<IdentityUser<Guid>> users,
    IAccessTokenIssuer issuer,
    TimeProvider clock) : IIdentityAuthentication
{
    private static readonly IdentityUser<Guid> DummyUser = new();
    private static readonly PasswordHasher<IdentityUser<Guid>> DummyHasher = new();
    private static readonly string DummyHash = DummyHasher.HashPassword(DummyUser, Guid.NewGuid().ToString());

    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticationInput.IsValid(request)) return null;
        var candidate = await users.FindByNameAsync(request.UserName!);
        if (candidate is null)
        {
            DummyHasher.VerifyHashedPassword(DummyUser, DummyHash, request.Password!);
            return null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(candidate.Id, cancellationToken);
        await db.Entry(candidate).ReloadAsync(cancellationToken);
        if (db.Entry(candidate).State == EntityState.Detached ||
            !db.Entry(candidate).Property<bool>("IsActive").CurrentValue || candidate.TwoFactorEnabled ||
            await users.IsLockedOutAsync(candidate))
        {
            DummyHasher.VerifyHashedPassword(DummyUser, DummyHash, request.Password!);
            return null;
        }

        if (!await users.CheckPasswordAsync(candidate, request.Password!))
        {
            EnsureSuccess(await users.AccessFailedAsync(candidate));
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (candidate.AccessFailedCount != 0) EnsureSuccess(await users.ResetAccessFailedCountAsync(candidate));
        if (string.IsNullOrWhiteSpace(candidate.SecurityStamp))
        {
            EnsureSuccess(await users.UpdateSecurityStampAsync(candidate));
        }
        var account = await GetAccountAsync(candidate, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var session = new LoginSession
        {
            Id = Guid.NewGuid(),
            UserId = candidate.Id,
            SecurityStamp = candidate.SecurityStamp!,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15)
        };
        var token = issuer.Issue(candidate.Id, session.Id, account.Roles, now, session.ExpiresAt);
        db.Set<LoginSession>().Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoginResult(token, session.ExpiresAt, account);
    }

    public async Task<CurrentAccount?> ValidateSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var user = await FindSessionUserAsync(userId, sessionId, cancellationToken);
        return user is null ? null : await GetAccountAsync(user, cancellationToken);
    }

    public async Task<AccountMutationResult> LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        if (await FindSessionUserAsync(userId, sessionId, cancellationToken) is null) return AccountMutationResult.Unauthorized;
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Set<LoginSession>().Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, (DateTime?)now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountMutationResult.Success;
    }

    public async Task<AccountMutationResult> ChangePasswordAsync(Guid userId, Guid sessionId,
        ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticationInput.IsValid(request) || request.CurrentPassword == request.NewPassword)
            return AccountMutationResult.InvalidPassword;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        var user = await FindSessionUserAsync(userId, sessionId, cancellationToken, trackUser: true);
        if (user is null) return AccountMutationResult.Unauthorized;
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword!);
        if (!result.Succeeded) return AccountMutationResult.InvalidPassword;
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Set<LoginSession>().Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, (DateTime?)now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountMutationResult.Success;
    }

    private async Task<IdentityUser<Guid>?> FindSessionUserAsync(Guid userId, Guid sessionId,
        CancellationToken cancellationToken, bool trackUser = false)
    {
        var now = clock.GetUtcNow();
        var query = from session in db.Set<LoginSession>()
                    join user in db.Users on session.UserId equals user.Id
                    where session.Id == sessionId && user.Id == userId && session.RevokedAt == null &&
                        session.ExpiresAt > now.UtcDateTime && session.SecurityStamp == user.SecurityStamp &&
                        EF.Property<bool>(user, "IsActive") && !user.TwoFactorEnabled &&
                        (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now)
                    select user;
        // UserManager updates require the same tracked user instance used by validation.
        return await (trackUser ? query.AsTracking() : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<CurrentAccount> GetAccountAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
    {
        var roles = await (from link in db.UserRoles
                           join role in db.Roles on link.RoleId equals role.Id
                           where link.UserId == user.Id
                           select role.Name!).OrderBy(n => n).ToArrayAsync(cancellationToken);
        var permissions = await (from link in db.UserRoles
                                 join grant in db.Set<RolePermission>() on link.RoleId equals grant.RoleId
                                 where link.UserId == user.Id
                                 select grant.PermissionCode)
            .Distinct().OrderBy(code => code).ToArrayAsync(cancellationToken);
        return new CurrentAccount(user.Id, user.UserName!, user.Email, roles, permissions);
    }

    private Task<int> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resource = "Restaurant.Identity.User." + userId.ToString("N");
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource},
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51001, 'Could not acquire Identity user lock.', 1;
            """, cancellationToken);
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Identity operation could not be persisted.");
    }
}
