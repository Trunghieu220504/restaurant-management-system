using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Messaging;

internal sealed class OutboxEntryConfiguration : IEntityTypeConfiguration<OutboxEntry>
{
    public void Configure(EntityTypeBuilder<OutboxEntry> entry)
    {
        entry.ToTable("Outbox", t =>
        {
            t.HasCheckConstraint("CK_Outbox_PhienBan", "[AggregateVersion] > 0 AND [SchemaVersion] > 0");
            t.HasCheckConstraint("CK_Outbox_LanThu", "[Attempts] >= 0");
        });
        entry.HasKey(x => x.Id);
        entry.Property(x => x.Id).ValueGeneratedNever();
        entry.Property(x => x.AggregateId).HasMaxLength(64).IsUnicode(false).IsRequired();
        entry.Property(x => x.Type).HasMaxLength(128).IsUnicode(false).IsRequired();
        entry.Property(x => x.Payload).IsRequired();
        entry.Property(x => x.LastError).HasMaxLength(1000);
        // The same fact (type + aggregate + version) cannot be staged twice.
        entry.HasIndex(x => new { x.Type, x.AggregateId, x.AggregateVersion }).IsUnique().HasDatabaseName("UX_Outbox_SuKien");
        // Dispatcher scan: only rows still waiting for delivery.
        entry.HasIndex(x => x.NextAttemptAt).HasDatabaseName("IX_Outbox_ChoGui").HasFilter("[ProcessedAt] IS NULL");
    }
}
