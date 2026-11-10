using Microsoft.EntityFrameworkCore;

namespace Restaurant.Modules.SalesDay.Infrastructure.Persistence;

public sealed class SalesDayDbContext(DbContextOptions<SalesDayDbContext> options)
    : DbContext(options)
{
    public DbSet<Restaurant.Modules.SalesDay.Domain.BusinessDay> Days => Set<Restaurant.Modules.SalesDay.Domain.BusinessDay>();

    public const string Schema = "salesday";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var day = modelBuilder.Entity<Restaurant.Modules.SalesDay.Domain.BusinessDay>();
        day.ToTable("NgayBan", Schema, t =>
        {
            t.HasCheckConstraint("CK_NgayBan_Cash", "[OpeningCash] >= 0");
            t.HasCheckConstraint("CK_NgayBan_State", "([Status] IN ('DANG_BAN','CHO_CHOT') AND [ActiveSlot] = 1 AND [ActiveSlot] IS NOT NULL) OR ([Status] = 'DA_CHOT' AND [ActiveSlot] IS NULL)");
        });
        day.HasKey(x => x.Id);
        day.Property(x => x.Status).HasMaxLength(16).IsRequired();
        day.Property(x => x.Version).IsRowVersion();
        day.HasIndex(x => x.BusinessDate).IsUnique();
        day.HasIndex(x => x.RequestKey).IsUnique();
        day.HasIndex(x => x.ActiveSlot).IsUnique().HasFilter("[ActiveSlot] IS NOT NULL");
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDayDbContext).Assembly);
    }
}
