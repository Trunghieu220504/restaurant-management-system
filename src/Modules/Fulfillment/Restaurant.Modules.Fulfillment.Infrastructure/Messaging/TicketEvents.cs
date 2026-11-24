using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Messaging;

public static class TicketEvents
{
    public const string Changed = "fulfillment.ticket_changed";

    public static TicketView View(PhieuCheBien x) => new(x.Id, x.ChiTietDonId, x.TramChot, x.TrangThai, x.TaoLuc,
        x.NguoiBatDauId, x.BatDauLuc, x.NguoiHoanThanhId, x.HoanThanhLuc, x.NguoiGiaoId, x.GiaoLuc,
        Convert.ToBase64String(x.Version));

    // Save the ticket first to obtain its SQL rowversion, then stage this row before committing the same transaction.
    public static void Stage(FulfillmentDbContext db, PhieuCheBien ticket, DateTimeOffset occurredAt)
    {
        if (db.Database.CurrentTransaction is null || ticket.Version.Length != 8)
            throw new InvalidOperationException("A persisted ticket and its active transaction are required.");
        db.Set<OutboxEntry>().Add(new OutboxEntry
        {
            Id = Guid.NewGuid(),
            // The existing int column cannot hold a SQL bigint rowversion. Encode that version in the unique key.
            AggregateId = $"ticket:{ticket.Id}:{Convert.ToHexString(ticket.Version)}",
            AggregateVersion = 1,
            Type = Changed,
            SchemaVersion = 1,
            Payload = IntegrationEventJson.SerializePayload(View(ticket)),
            OccurredAt = occurredAt.UtcDateTime,
            NextAttemptAt = occurredAt.UtcDateTime
        });
    }
}
