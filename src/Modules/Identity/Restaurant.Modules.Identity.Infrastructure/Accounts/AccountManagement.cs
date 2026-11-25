using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Identity.Application.Accounts;
using Restaurant.Modules.Identity.Application.Authorization;
using Restaurant.Modules.Identity.Domain;
using Restaurant.Modules.Identity.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Identity.Infrastructure.Accounts;

/// <summary>
/// HIE-5.2. Every write runs in one transaction that first takes the account-administration lock, then re-reads what the
/// caller may do from the database (an Admin demoted a moment ago loses the right at once), then locks the target account.
/// Two Admins can therefore never disable each other at the same time and leave nobody able to manage accounts.
/// </summary>
public sealed class AccountManagement(
    IdentityDbContext db, UserManager<IdentityUser<Guid>> users, TimeProvider clock, IBusinessAudit audit) : IAccountManagement
{
    private const int MaxText = 256;
    private const int MaxPassword = 1024;

    private enum Scope { None, Subordinates, All }

    private sealed record Target(IdentityUser<Guid> User, string[] Roles, Scope Scope);

    public async Task<AccountResult<AccountPage>> ListAsync(Guid actorId, AccountListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Offset < 0 || query.Limit < 1 || query.Limit > AccountListQuery.MaxLimit ||
            (query.Role is not null && !AccountRoles.All.Contains(query.Role, StringComparer.Ordinal)))
            return Fail<AccountPage>(AccountErrors.InputInvalid);
        var scope = await ScopeOfAsync(actorId, cancellationToken);
        if (scope == Scope.None) return Fail<AccountPage>(AccountErrors.Forbidden);

        var accounts = db.Users.AsNoTracking();
        if (scope == Scope.Subordinates)
        {
            var allowed = AccountRoles.Subordinate.ToArray();
            // The whole role set must be subordinate, so an account that also holds Manager stays out of reach.
            accounts = accounts.Where(u => !(from link in db.UserRoles
                                             join role in db.Roles on link.RoleId equals role.Id
                                             where link.UserId == u.Id && !allowed.Contains(role.Name!)
                                             select link.UserId).Any());
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            accounts = accounts.Where(u => u.UserName!.Contains(term) || (u.Email != null && u.Email.Contains(term)));
        }
        if (query.Role is not null)
            accounts = accounts.Where(u => (from link in db.UserRoles
                                            join role in db.Roles on link.RoleId equals role.Id
                                            where link.UserId == u.Id && role.Name == query.Role
                                            select link.UserId).Any());
        if (query.Active is { } active)
            accounts = accounts.Where(u => EF.Property<bool>(u, "IsActive") == active);

        var total = await accounts.CountAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var page = await accounts.OrderBy(u => u.UserName).ThenBy(u => u.Id).Skip(query.Offset).Take(query.Limit)
            .Select(u => new { u.Id, u.UserName, u.Email, u.LockoutEnd, Active = EF.Property<bool>(u, "IsActive") })
            .ToListAsync(cancellationToken);
        var ids = page.Select(x => x.Id).ToArray();
        var roles = (await (from link in db.UserRoles
                            join role in db.Roles on link.RoleId equals role.Id
                            where ids.Contains(link.UserId)
                            select new { link.UserId, Name = role.Name! }).ToListAsync(cancellationToken))
            .ToLookup(x => x.UserId, x => x.Name);
        var items = page.Select(x => new AccountDto(x.Id, x.UserName!, x.Email,
            [.. roles[x.Id].Order(StringComparer.Ordinal)], x.Active, x.LockoutEnd > now)).ToList();
        return AccountResult<AccountPage>.Ok(new AccountPage(items, total));
    }

    public async Task<AccountResult<AccountDto>> GetAsync(Guid actorId, Guid accountId, CancellationToken cancellationToken)
    {
        var scope = await ScopeOfAsync(actorId, cancellationToken);
        if (scope == Scope.None) return Fail<AccountDto>(AccountErrors.Forbidden);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == accountId, cancellationToken);
        if (user is null) return Fail<AccountDto>(AccountErrors.NotFound);
        var roles = await RolesOfAsync(accountId, cancellationToken);
        if (!InScope(scope, roles)) return Fail<AccountDto>(AccountErrors.OutOfScope);
        return AccountResult<AccountDto>.Ok(await ToDtoAsync(user, roles, cancellationToken));
    }

    public async Task<AccountResult<AccountDto>> CreateAsync(Guid actorId, CreateAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsValidName(request.UserName) || !IsValidText(request.Email) || !IsPasswordShape(request.Password) ||
            !TryNormalizeRoles(request.Roles, out var roles))
            return Fail<AccountDto>(AccountErrors.InputInvalid);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockAccountAdministrationAsync(cancellationToken);
        var scope = await ScopeOfAsync(actorId, cancellationToken);
        if (scope == Scope.None) return Fail<AccountDto>(AccountErrors.Forbidden);
        if (scope == Scope.Subordinates && roles.Any(r => !AccountRoles.Subordinate.Contains(r)))
            return Fail<AccountDto>(AccountErrors.RoleNotAllowed);

        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = request.UserName, Email = request.Email! };
        var created = await users.CreateAsync(user, request.Password!);
        if (!created.Succeeded) return Fail<AccountDto>(MapCreateError(created));
        EnsureSuccess(await users.AddToRolesAsync(user, roles));
        await audit.AppendAsync(new BusinessAuditEntry("Identity", "account.create", "Account", user.Id.ToString(), actorId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountResult<AccountDto>.Ok(new AccountDto(user.Id, user.UserName!, user.Email, roles, true, false));
    }

    public async Task<AccountResult<AccountDto>> SetRolesAsync(
        Guid actorId, Guid accountId, SetRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalizeRoles(request.Roles, out var wanted)) return Fail<AccountDto>(AccountErrors.InputInvalid);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (target, error) = await OpenTargetAsync(actorId, accountId, forbidSelf: true, cancellationToken);
        if (target is null) return Fail<AccountDto>(error!);
        if (target.Scope == Scope.Subordinates && wanted.Any(r => !AccountRoles.Subordinate.Contains(r)))
            return Fail<AccountDto>(AccountErrors.RoleNotAllowed);

        var remove = target.Roles.Except(wanted).ToArray();
        var add = wanted.Except(target.Roles).ToArray();
        if (remove.Length > 0) EnsureSuccess(await users.RemoveFromRolesAsync(target.User, remove));
        if (add.Length > 0) EnsureSuccess(await users.AddToRolesAsync(target.User, add));
        if (remove.Length > 0 || add.Length > 0)
            await audit.AppendAsync(new BusinessAuditEntry("Identity", "account.roles", "Account", accountId.ToString(), actorId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // No session is touched: roles are read from the database on every request, so open tokens lose or gain rights at once.
        return AccountResult<AccountDto>.Ok(await ToDtoAsync(target.User, wanted, cancellationToken));
    }

    public async Task<AccountResult<AccountDto>> SetActiveAsync(
        Guid actorId, Guid accountId, bool active, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (target, error) = await OpenTargetAsync(actorId, accountId, forbidSelf: true, cancellationToken);
        if (target is null) return Fail<AccountDto>(error!);

        var entry = db.Entry(target.User);
        if (entry.Property<bool>("IsActive").CurrentValue != active)
        {
            entry.Property<bool>("IsActive").CurrentValue = active;
            await db.SaveChangesAsync(cancellationToken);
            if (!active) await RevokeAllSessionsAsync(accountId, cancellationToken);
            await audit.AppendAsync(new BusinessAuditEntry("Identity", active ? "account.activate" : "account.deactivate",
                "Account", accountId.ToString(), actorId), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return AccountResult<AccountDto>.Ok(await ToDtoAsync(target.User, target.Roles, cancellationToken));
    }

    public async Task<AccountResult<AccountDto>> ResetPasswordAsync(
        Guid actorId, Guid accountId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsPasswordShape(request.NewPassword)) return Fail<AccountDto>(AccountErrors.InputInvalid);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (target, error) = await OpenTargetAsync(actorId, accountId, forbidSelf: true, cancellationToken);
        if (target is null) return Fail<AccountDto>(error!);

        // Checked first so a weak password never reaches the remove-then-add step below.
        foreach (var validator in users.PasswordValidators)
            if (!(await validator.ValidateAsync(users, target.User, request.NewPassword!)).Succeeded)
                return Fail<AccountDto>(AccountErrors.PasswordInvalid);

        if (await users.HasPasswordAsync(target.User)) EnsureSuccess(await users.RemovePasswordAsync(target.User));
        EnsureSuccess(await users.AddPasswordAsync(target.User, request.NewPassword!));
        // A reset is also how a locked-out account is recovered.
        if (target.User.LockoutEnabled) EnsureSuccess(await users.SetLockoutEndDateAsync(target.User, null));
        EnsureSuccess(await users.ResetAccessFailedCountAsync(target.User));
        await RevokeAllSessionsAsync(accountId, cancellationToken);
        await audit.AppendAsync(new BusinessAuditEntry("Identity", "account.reset-password", "Account", accountId.ToString(), actorId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AccountResult<AccountDto>.Ok(await ToDtoAsync(target.User, target.Roles, cancellationToken));
    }

    /// <summary>Locks, checks what the caller may do, finds the target and checks it is within the caller's reach.</summary>
    private async Task<(Target? Target, string? Error)> OpenTargetAsync(
        Guid actorId, Guid accountId, bool forbidSelf, CancellationToken cancellationToken)
    {
        await db.LockAccountAdministrationAsync(cancellationToken);
        var scope = await ScopeOfAsync(actorId, cancellationToken);
        if (scope == Scope.None) return (null, AccountErrors.Forbidden);
        if (forbidSelf && actorId == accountId) return (null, AccountErrors.SelfChange);
        await db.LockUserAsync(accountId, cancellationToken);
        var user = await db.Users.AsTracking().SingleOrDefaultAsync(u => u.Id == accountId, cancellationToken);
        if (user is null) return (null, AccountErrors.NotFound);
        var roles = await RolesOfAsync(accountId, cancellationToken);
        return InScope(scope, roles) ? (new Target(user, roles, scope), null) : (null, AccountErrors.OutOfScope);
    }

    /// <summary>All accounts for those holding accounts.manage, subordinates for subordinates.manage; none for a disabled caller.</summary>
    private async Task<Scope> ScopeOfAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var active = await db.Users.AsNoTracking().AnyAsync(u => u.Id == actorId && EF.Property<bool>(u, "IsActive"), cancellationToken);
        if (!active) return Scope.None;
        var codes = await (from link in db.UserRoles
                           join grant in db.Set<RolePermission>() on link.RoleId equals grant.RoleId
                           where link.UserId == actorId && (grant.PermissionCode == PermissionCodes.IdentityAccountsManage ||
                                                            grant.PermissionCode == PermissionCodes.IdentitySubordinatesManage)
                           select grant.PermissionCode).Distinct().ToListAsync(cancellationToken);
        return codes.Contains(PermissionCodes.IdentityAccountsManage) ? Scope.All
            : codes.Contains(PermissionCodes.IdentitySubordinatesManage) ? Scope.Subordinates : Scope.None;
    }

    private static bool InScope(Scope scope, string[] targetRoles) => scope switch
    {
        Scope.All => true,
        Scope.Subordinates => targetRoles.All(r => AccountRoles.Subordinate.Contains(r)),
        _ => false
    };

    private async Task<string[]> RolesOfAsync(Guid userId, CancellationToken cancellationToken) =>
        await (from link in db.UserRoles
               join role in db.Roles on link.RoleId equals role.Id
               where link.UserId == userId
               select role.Name!).OrderBy(n => n).ToArrayAsync(cancellationToken);

    private async Task<AccountDto> ToDtoAsync(IdentityUser<Guid> user, string[] roles, CancellationToken cancellationToken)
    {
        var active = await db.Users.AsNoTracking().Where(u => u.Id == user.Id)
            .Select(u => EF.Property<bool>(u, "IsActive")).SingleAsync(cancellationToken);
        return new AccountDto(user.Id, user.UserName!, user.Email, roles, active, user.LockoutEnd > clock.GetUtcNow());
    }

    private async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Set<LoginSession>().Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, (DateTime?)now), cancellationToken);
        await db.Set<RefreshToken>().Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, (DateTime?)now), cancellationToken);
    }

    /// <summary>Same shape rules login applies, so an account can always be signed in with what was stored.</summary>
    private static bool IsValidName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxText && value == value.Trim();

    private static bool IsValidText(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxText;

    private static bool IsPasswordShape(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxPassword;

    /// <summary>At least one role, no duplicates, only the five fixed names (exact spelling).</summary>
    private static bool TryNormalizeRoles(string[]? requested, out string[] roles)
    {
        roles = [];
        if (requested is null || requested.Length == 0 || requested.Length > AccountRoles.All.Count) return false;
        if (requested.Any(r => r is null || !AccountRoles.All.Contains(r, StringComparer.Ordinal))) return false;
        if (requested.Distinct(StringComparer.Ordinal).Count() != requested.Length) return false;
        roles = [.. requested.Order(StringComparer.Ordinal)];
        return true;
    }

    private static string MapCreateError(IdentityResult result)
    {
        var codes = result.Errors.Select(e => e.Code).ToArray();
        if (codes.Any(c => c is nameof(IdentityErrorDescriber.InvalidUserName) or nameof(IdentityErrorDescriber.InvalidEmail)))
            return AccountErrors.InputInvalid;
        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName))) return AccountErrors.UserNameTaken;
        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail))) return AccountErrors.EmailTaken;
        if (codes.Any(c => c.StartsWith("Password", StringComparison.Ordinal))) return AccountErrors.PasswordInvalid;
        throw new InvalidOperationException("Account could not be created: " + string.Join(", ", codes));
    }

    private static AccountResult<T> Fail<T>(string error) where T : class => AccountResult<T>.Fail(error);

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Identity operation could not be persisted: " +
                string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
