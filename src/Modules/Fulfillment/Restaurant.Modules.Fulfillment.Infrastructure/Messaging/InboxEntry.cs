using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Messaging;

/// <summary>
/// Technical Inbox row (not a domain entity): "this consumer already handled this event". It is written only by
/// <c>InboxStore.TryRecordAsync</c> (Restaurant.Shared.SqlServer) inside the consumer's own transaction, never through EF,
/// so this class exists to let the migration create the table with the shape that code expects.
/// </summary>
internal sealed class InboxEntry
{
    public string Consumer { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

internal sealed class InboxEntryConfiguration : IEntityTypeConfiguration<InboxEntry>
{
    public void Configure(EntityTypeBuilder<InboxEntry> entry)
    {
        entry.ToTable("Inbox");
        entry.HasKey(x => new { x.Consumer, x.EventId }).HasName("PK_Inbox");
        entry.Property(x => x.Consumer).HasMaxLength(100).IsUnicode(false).IsRequired();
        entry.Property(x => x.ProcessedAt).HasColumnType("datetime2");
    }
}
