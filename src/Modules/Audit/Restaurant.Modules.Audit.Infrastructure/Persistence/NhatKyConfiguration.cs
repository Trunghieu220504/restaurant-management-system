using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Restaurant.Modules.Audit.Domain;

namespace Restaurant.Modules.Audit.Infrastructure.Persistence;

public sealed class NhatKyConfiguration : IEntityTypeConfiguration<NhatKy>
{
    public void Configure(EntityTypeBuilder<NhatKy> builder)
    {
        builder.ToTable("NhatKy", table =>
        {
            table.HasTrigger("TR_NhatKy_AppendOnly");
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_NhatKy_Utc", "DATEPART(TZOFFSET,[OccurredAtUtc])=0 AND DATEPART(TZOFFSET,[RecordedAtUtc])=0");
        });
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).ValueGeneratedNever();
        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.RecordedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.SourceModule).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(512);
        builder.HasIndex(x => new { x.OccurredAtUtc, x.EventId }).IsDescending(true, true);
        builder.HasIndex(x => new { x.ActorId, x.OccurredAtUtc, x.EventId }).IsDescending(false, true, true);
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc, x.EventId }).IsDescending(false, false, true, true);
    }
}
