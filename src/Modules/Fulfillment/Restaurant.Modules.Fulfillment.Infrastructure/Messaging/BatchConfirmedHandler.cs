using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Restaurant.Modules.Fulfillment.Domain;
using Restaurant.Modules.Sales.Contracts;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;
using Restaurant.Shared.SqlServer;

namespace Restaurant.Modules.Fulfillment.Infrastructure.Messaging;

internal sealed class BatchConfirmedHandler(
    FulfillmentDbContext db,
    TimeProvider clock) : IIntegrationEventHandler
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

        foreach (var portion in envelope.Payload.Portions)
        {
            var result = PhieuCheBien.CreateInitial(portion.PortionId, portion.Station, envelope.OccurredAt.UtcDateTime);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Invalid confirmed portion {portion.PortionId}: {result.Error}.");

            db.Tickets.Add(result.Value!);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
