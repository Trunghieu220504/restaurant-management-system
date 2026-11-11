using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Identity.Domain;

namespace Restaurant.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options)
{
    public const string Schema = "identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        // New IdentityUser instances have no CLR activity property; let SQL supply true.
        modelBuilder.Entity<IdentityUser<Guid>>().Property<bool>("IsActive").HasDefaultValue(true).HasSentinel(false);
        modelBuilder.Entity<LoginSession>(entity =>
        {
            entity.ToTable("LoginSession", t => t.HasCheckConstraint("CK_LoginSession_Expiry", "[ExpiresAt] > [CreatedAt]"));
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SecurityStamp).HasMaxLength(256).IsRequired();
            entity.Property(s => s.RowVersion).IsRowVersion();
            entity.HasIndex(s => new { s.UserId, s.RevokedAt });
            entity.HasIndex(s => s.ExpiresAt);
            entity.HasOne<IdentityUser<Guid>>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken", t => t.HasCheckConstraint("CK_RefreshToken_Expiry", "[ExpiresAt] > [CreatedAt]"));
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(32).IsFixedLength().IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.SessionId);
            entity.HasIndex(t => t.ExpiresAt);
            entity.HasOne<LoginSession>().WithMany().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IdentityUser<Guid>>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permission");
            entity.HasKey(p => p.Code);
            entity.Property(p => p.Code).HasMaxLength(100).IsUnicode(false);
            entity.Property(p => p.Name).HasMaxLength(150).IsRequired();
        });
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermission");
            entity.HasKey(p => new { p.RoleId, p.PermissionCode });
            entity.Property(p => p.PermissionCode).HasMaxLength(100).IsUnicode(false);
            entity.HasOne<IdentityRole<Guid>>().WithMany().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Permission>().WithMany().HasForeignKey(p => p.PermissionCode).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AdminBootstrap>(entity =>
        {
            entity.ToTable("AdminBootstrap", t => t.HasCheckConstraint("CK_AdminBootstrap_Singleton", "[Id] = 1"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.Property(p => p.CreatedAt).HasColumnType("datetime2");
            entity.HasIndex(p => p.UserId).IsUnique();
            entity.HasOne<IdentityUser<Guid>>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        IdentityCatalog.Seed(modelBuilder);
    }
}
