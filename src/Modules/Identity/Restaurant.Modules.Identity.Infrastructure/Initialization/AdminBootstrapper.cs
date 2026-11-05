using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Identity.Infrastructure.Persistence;

namespace Restaurant.Modules.Identity.Infrastructure.Initialization;

public sealed class AdminBootstrapper(IdentityDbContext db, UserManager<IdentityUser<Guid>> users)
{
    public async Task InitializeAsync(AdminBootstrapInput input, CancellationToken cancellationToken = default)
    {
        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(),
            UserName = input.UserName,
            Email = input.Email,
            EmailConfirmed = false,
            LockoutEnabled = true
        };
        foreach (var validator in users.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(users, user, input.Password)).Succeeded)
            {
                throw new InvalidOperationException("Bootstrap password does not satisfy the configured password policy.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // This lock is shared by all bootstrap command instances, not by web requests.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'Restaurant.Identity.AdminBootstrap',
                @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51000, 'Could not acquire Identity bootstrap lock.', 1;
            """, cancellationToken);

        if (await db.Set<AdminBootstrap>().AnyAsync(cancellationToken) ||
            await db.UserRoles.AnyAsync(r => r.RoleId == IdentityCatalog.AdminRoleId, cancellationToken))
        {
            throw new InvalidOperationException("Admin bootstrap has already been completed or an admin exists.");
        }
        if (!await db.Roles.AnyAsync(r => r.Id == IdentityCatalog.AdminRoleId && r.Name == "Admin" &&
                r.NormalizedName == "ADMIN", cancellationToken))
        {
            throw new InvalidOperationException("Apply the reviewed Identity migration before bootstrap.");
        }
        if (await users.FindByNameAsync(input.UserName) is not null || await users.FindByEmailAsync(input.Email) is not null)
        {
            throw new InvalidOperationException("Bootstrap will not modify or elevate an existing account.");
        }

        EnsureSuccess(await users.CreateAsync(user, input.Password));
        EnsureSuccess(await users.AddToRoleAsync(user, "Admin"));
        db.Set<AdminBootstrap>().Add(new AdminBootstrap { Id = 1, UserId = user.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Identity bootstrap could not be completed; no changes were committed.");
        }
    }
}
