using System.Text.Json;
using Restaurant.Modules.SalesDay.Domain;
using Restaurant.Modules.SalesDay.Infrastructure.Persistence;

namespace Restaurant.Modules.SalesDay.Infrastructure.Messaging;

public static class DayClosedEvent
{
    public static void Enqueue(SalesDayDbContext db, DaySnapshot snapshot)
    {
        db.Add(new OutboxEntry
        {
            Id = Guid.NewGuid(),
            AggregateId = snapshot.DayId.ToString("D"),
            AggregateVersion = snapshot.SnapshotVersion,
            Type = "salesday.day.closed",
            SchemaVersion = 1,
            OccurredAt = snapshot.ClosedAt.UtcDateTime,
            NextAttemptAt = snapshot.ClosedAt.UtcDateTime,
            Payload = JsonSerializer.Serialize(new { businessDayId = snapshot.DayId, snapshotVersion = snapshot.SnapshotVersion })
        });
    }
}
