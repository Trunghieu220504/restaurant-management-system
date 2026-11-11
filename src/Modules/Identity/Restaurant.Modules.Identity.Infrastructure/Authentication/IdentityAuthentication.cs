using System.Security.Cryptography;
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
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    /// <summary>Absolute cap from login: rotation never extends it, so a stolen cookie cannot live forever.</summary>
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
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
            ExpiresAt = now.Add(AccessTokenLifetime)
        };
        var token = issuer.Issue(candidate.Id, session.Id, account.Roles, now, session.ExpiresAt);
        db.Set<LoginSession>().Add(session);
        var (refreshSecret, refresh) = NewRefreshToken(session.Id, candidate.Id, now, now.Add(RefreshTokenLifetime));
        db.Set<RefreshToken>().Add(refresh);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoginResult(token, session.ExpiresAt, account)
        {
            RefreshToken = refreshSecret,
            RefreshTokenExpiresAt = refresh.ExpiresAt
        };
    }

    public async Task<LoginResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 128) return null;
        var hash = HashSecret(refreshToken);
        var ownerId = await db.Set<RefreshToken>().AsNoTracking().Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.UserId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(ownerId.Value, cancellationToken);
        // Re-read under the user lock so two concurrent refreshes with the same secret are serialized.
        var current = await db.Set<RefreshToken>().AsTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (current is null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        if (current.UsedAt is not null)
        {
            if (current.RevokedAt is not null) return null;
            // An already-rotated secret came back: treat as theft and end the whole chain (= its session).
            await RevokeSessionAsync(current.SessionId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (current.RevokedAt is not null || current.ExpiresAt <= now) return null;
        var user = await FindSessionUserAsync(current.UserId, current.SessionId, cancellationToken, allowExpiredSession: true);
        if (user is null) return null;

        var account = await GetAccountAsync(user, cancellationToken);
        var accessExpires = now.Add(AccessTokenLifetime);
        var (secret, next) = NewRefreshToken(current.SessionId, current.UserId, now, current.ExpiresAt);
        current.UsedAt = now;
        current.ReplacedByTokenId = next.Id;
        db.Set<RefreshToken>().Add(next);
        await db.Set<LoginSession>().Where(s => s.Id == current.SessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ExpiresAt, accessExpires), cancellationToken);
        var access = issuer.Issue(current.UserId, current.SessionId, account.Roles, now, accessExpires);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoginResult(access, accessExpires, account) { RefreshToken = secret, RefreshTokenExpiresAt = next.ExpiresAt };
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
        await RevokeSessionAsync(sessionId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountMutationResult.Success;
    }

    public async Task<AccountMutationResult> LogoutByRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 128) return AccountMutationResult.Unauthorized;
        var hash = HashSecret(refreshToken);
        var ownerId = await db.Set<RefreshToken>().AsNoTracking().Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.UserId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId is null) return AccountMutationResult.Unauthorized;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(ownerId.Value, cancellationToken);
        var token = await db.Set<RefreshToken>().AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (token is null || token.ExpiresAt <= now) return AccountMutationResult.Unauthorized;
        // Even a consumed cookie may end its own session, but can never issue new credentials.
        await RevokeSessionAsync(token.SessionId, now, cancellationToken);
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
        await db.Set<RefreshToken>().Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, (DateTime?)now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountMutationResult.Success;
    }

    private async Task<IdentityUser<Guid>?> FindSessionUserAsync(Guid userId, Guid sessionId,
        CancellationToken cancellationToken, bool trackUser = false, bool allowExpiredSession = false)
    {
        var now = clock.GetUtcNow();
        var query = from session in db.Set<LoginSession>()
                    join user in db.Users on session.UserId equals user.Id
                    where session.Id == sessionId && user.Id == userId && session.RevokedAt == null &&
                        (allowExpiredSession || session.ExpiresAt > now.UtcDateTime) && session.SecurityStamp == user.SecurityStamp &&
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

    private async Task RevokeSessionAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken)
    {
        await db.Set<LoginSession>().Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, (DateTime?)now), cancellationToken);
        await db.Set<RefreshToken>().Where(t => t.SessionId == sessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, (DateTime?)now), cancellationToken);
    }

    private static (string Secret, RefreshToken Entity) NewRefreshToken(Guid sessionId, Guid userId, DateTime now, DateTime expiresAt)
    {
        var secret = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (secret, new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            TokenHash = HashSecret(secret),
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
    }

    private static byte[] HashSecret(string secret) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
