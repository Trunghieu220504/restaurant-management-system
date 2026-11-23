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
        var closing = modelBuilder.Entity<Restaurant.Modules.SalesDay.Domain.ClosingEntry>();
        closing.ToTable("ClosingHistory", Schema);
        closing.HasKey(x => new { x.DayId, x.RequestKey });
        closing.HasOne<Restaurant.Modules.SalesDay.Domain.BusinessDay>().WithMany().HasForeignKey(x => x.DayId);
        closing.Property(x => x.Status).HasMaxLength(16);
        var snapshot = modelBuilder.Entity<Restaurant.Modules.SalesDay.Domain.DaySnapshot>();
        snapshot.ToTable("TongHopNgay", Schema);
        snapshot.HasKey(x => x.DayId);
        snapshot.HasOne<Restaurant.Modules.SalesDay.Domain.BusinessDay>().WithOne().HasForeignKey<Restaurant.Modules.SalesDay.Domain.DaySnapshot>(x => x.DayId);
        snapshot.Property(x => x.LateReason).HasMaxLength(1000);
        snapshot.Property(x => x.DifferenceReason).HasMaxLength(1000);
        foreach (var property in snapshot.Metadata.GetProperties().Where(x => x.ClrType == typeof(decimal)))
            snapshot.Property(property.Name).HasPrecision(28, property.Name == "RoundingDifference" ? 8 : 0);
        var method = modelBuilder.Entity<Restaurant.Modules.SalesDay.Domain.DayMethodSnapshot>();
        method.ToTable("TongHopNgayPhuongThuc", Schema);
        method.HasKey(x => new { x.DayId, x.Method });
        method.Property(x => x.Method).HasMaxLength(32);
        method.Property(x => x.Receipts).HasPrecision(28, 0);
        method.Property(x => x.Refunds).HasPrecision(28, 0);
        method.Property(x => x.Net).HasPrecision(28, 0);
        snapshot.HasMany(x => x.Methods).WithOne().HasForeignKey(x => x.DayId);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDayDbContext).Assembly);
    }
}
