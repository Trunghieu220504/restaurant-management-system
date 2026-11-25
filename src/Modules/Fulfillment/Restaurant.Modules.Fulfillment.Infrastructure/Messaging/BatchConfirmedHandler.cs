using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Restaurant.Shared.SqlServer;
using Restaurant.Modules.Fulfillment.Application;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Messaging;

internal sealed class BatchConfirmedHandler(
    FulfillmentDbContext db,
    TimeProvider clock, IFulfillmentIncidentContext businessContext, IBusinessAudit audit) : IIntegrationEventHandler
{
    public const string EventName = "sales.batch_confirmed";
    public const string ConsumerName = "fulfillment.tickets";

    public string EventType => EventName;
    public string Consumer => ConsumerName;

    public async Task HandleAsync(StoredIntegrationEvent message, CancellationToken cancellationToken)
    {
        var envelope = IntegrationEventJson.Read<BatchConfirmed>(message);
        if (envelope.Version != 1)
            throw new InvalidOperationException($"Unsupported {EventName} schema version {envelope.Version}.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (!await InboxStore.TryRecordAsync(connection, transaction.GetDbTransaction(), FulfillmentDbContext.Schema, Consumer,
                message.EventId, clock.GetUtcNow(), cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var tickets = new List<PhieuCheBien>();
        foreach (var portion in envelope.Payload.Portions)
        {
            var error = await businessContext.RequireOpenAsync(transaction.GetDbTransaction(), portion.PortionId, cancellationToken);
            if (error == "sales.portion.cancelled") continue;
            if (error is not null) throw new InvalidOperationException(error);
            var result = PhieuCheBien.CreateInitial(portion.PortionId, portion.Station, envelope.OccurredAt.UtcDateTime);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Invalid confirmed portion {portion.PortionId}: {result.Error}.");

            db.Tickets.Add(result.Value!);
            tickets.Add(result.Value!);
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var ticket in tickets) TicketEvents.Stage(db, ticket, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        foreach (var ticket in tickets)
            await audit.AppendAsync(new("Fulfillment", "fulfillment.ticket.created", "PhieuCheBien",
                ticket.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ResolveActor: false), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
