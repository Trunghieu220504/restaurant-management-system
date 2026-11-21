namespace Restaurant.Modules.Audit.Domain;

/// <summary>An immutable business audit record, identified by the producer's stable event ID.</summary>
public sealed class NhatKy
{
    private static readonly HashSet<string> Modules = new(StringComparer.Ordinal)
    {
        "Identity", "Seating", "Catalog", "Sales", "Fulfillment", "Billing", "Inventory",
        "SalesDay", "Audit", "Reporting", "System"
    };

    private NhatKy() { }

    public Guid EventId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public Guid? ActorId { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public Guid? BusinessDayId { get; private set; }
    public string? Reason { get; private set; }

    public static NhatKy Create(Guid eventId, DateTimeOffset occurredAtUtc, DateTimeOffset recordedAtUtc,
        Guid? actorId, string sourceModule, string action, string entityType, string entityId,
        Guid? businessDayId, string? reason)
    {
        if (eventId == Guid.Empty || occurredAtUtc == default || occurredAtUtc.Offset != TimeSpan.Zero ||
            recordedAtUtc == default || recordedAtUtc.Offset != TimeSpan.Zero || actorId == Guid.Empty ||
            businessDayId == Guid.Empty || !Modules.Contains(sourceModule) ||
            (actorId is null && sourceModule != "System") || !ValidText(action, 100) ||
            !ValidText(entityType, 100) || !ValidText(entityId, 100) ||
            (reason is not null && (reason.Length > 512 || reason.Any(char.IsControl))))
            throw new ArgumentException("Invalid audit message; use bounded business identifiers and UTC timestamps.");

        return new NhatKy
        {
            EventId = eventId,
            OccurredAtUtc = occurredAtUtc,
            RecordedAtUtc = recordedAtUtc,
            ActorId = actorId,
            SourceModule = sourceModule,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BusinessDayId = businessDayId,
            Reason = reason
        };
    }

    public static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim() &&
        !value.Any(char.IsControl);
}
