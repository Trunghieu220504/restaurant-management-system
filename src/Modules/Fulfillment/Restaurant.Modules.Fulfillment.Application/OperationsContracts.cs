namespace Restaurant.Modules.Fulfillment.Application;

public sealed record TerminalTicket(long Id, string Version);
public sealed record OperationsSnapshot(IReadOnlyList<TicketView> Tickets, IReadOnlyList<TerminalTicket> Tombstones);

public interface IOperationsSnapshot
{
    Task<OperationsSnapshot> ReadAsync(IReadOnlyCollection<string> stations, long? orderId, CancellationToken ct);
}

public sealed record TicketChanged(Guid EventId, DateTimeOffset OccurredAt, string Version, TicketView Payload);
