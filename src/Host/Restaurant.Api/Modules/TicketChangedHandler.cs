using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Application;
using Restaurant.Modules.Fulfillment.Infrastructure.Messaging;
using Restaurant.Modules.Identity.Application.Authentication;
using Restaurant.Modules.Sales.Infrastructure.Persistence;
using Restaurant.Shared.Contracts;

namespace Restaurant.Api.Modules;

public sealed class TicketChangedHandler(
    IHubContext<OperationsHub> hub, OperationsConnections connections, IIdentityAuthentication identity,
    SalesDbContext sales, TimeProvider clock) : IIntegrationEventHandler
{
    public string EventType => TicketEvents.Changed;
    public string Consumer => "operations.signalr";

    public async Task HandleAsync(StoredIntegrationEvent message, CancellationToken cancellationToken)
    {
        if (message.Type != EventType || message.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported ticket event.");
        var ticket = IntegrationEventJson.Read<TicketView>(message).Payload;
        var orderId = await (from portion in sales.Portions
                             join batch in sales.Batches on portion.DotGoiMonId equals batch.Id
                             where portion.Id == ticket.PortionId
                             select (long?)batch.DonHangId).SingleOrDefaultAsync(cancellationToken);
        var recipients = new List<string>();
        foreach (var (id, connection) in connections.Active)
        {
            if (connection.ExpiresAt <= clock.GetUtcNow()) continue;
            var groups = connection.Groups;
            if (!groups.ContainsKey(OperationsConnections.Station(ticket.Station)) &&
                !groups.ContainsKey(OperationsConnections.Serving) &&
                !(orderId.HasValue && groups.ContainsKey(OperationsConnections.Order(orderId.Value)))) continue;
            // Existing sockets must stop receiving data as soon as their account/session or role is revoked.
            var account = await identity.ValidateSessionAsync(connection.UserId, connection.SessionId, cancellationToken);
            if (account is null) continue;
            var user = OperationsHub.Principal(account);
            if (OperationsAccess.CanReadStation(user, ticket.Station)) recipients.Add(id);
        }
        if (recipients.Count == 0) return;
        await hub.Clients.Clients(recipients).SendAsync("TicketChanged",
            new TicketChanged(message.EventId, message.OccurredAt, ticket.Version, ticket), cancellationToken);
    }
}
