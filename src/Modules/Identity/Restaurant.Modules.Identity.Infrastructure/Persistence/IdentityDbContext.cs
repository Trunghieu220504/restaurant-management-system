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
